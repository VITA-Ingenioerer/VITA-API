using Vita.Planning.Application.Exceptions;
using Vita.Planning.Application.Interfaces;

namespace Vita.Planning.Infrastructure.Services;

public sealed class EconomicProjectNumberAllocator : IEconomicProjectNumberAllocator
{
    private const int MaxAttempts = 10;
    private const int MaxSubProjectOffset = 99;

    private readonly IEconomicProjectSourceClient _sourceClient;
    private readonly IEconomicProjectWriteClient _writeClient;

    public EconomicProjectNumberAllocator(
        IEconomicProjectSourceClient sourceClient,
        IEconomicProjectWriteClient writeClient)
    {
        _sourceClient = sourceClient;
        _writeClient = writeClient;
    }

    public async Task<int> CreateMainProjectAsync(
        string name,
        int projectGroupNumber,
        int? customerNumber,
        int? responsibleEmployeeNumber,
        CancellationToken cancellationToken = default)
    {
        var year = DateTime.UtcNow.Year % 100;
        var rangeMin = year * 1_000_000;
        var rangeMax = rangeMin + 999_999;

        var maxNumber = await GetMaxProjectNumberInRangeAsync(rangeMin, rangeMax, cancellationToken);
        var nextCounter = maxNumber is null
            ? 1
            : (maxNumber.Value % 1_000_000) / 100 + 1;

        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            var counter = nextCounter + attempt;
            if (counter > 9999)
                throw new InvalidOperationException($"No more project numbers available for year 20{year:D2}.");

            var candidate = rangeMin + counter * 100;

            try
            {
                await _writeClient.CreateProjectAsync(
                    projectNumber: candidate,
                    name: name,
                    projectGroupNumber: projectGroupNumber,
                    customerNumber: customerNumber,
                    responsibleEmployeeNumber: responsibleEmployeeNumber,
                    isMainProject: true,
                    mainProjectNumber: null,
                    cancellationToken: cancellationToken);

                return candidate;
            }
            catch (EconomicProjectNumberConflictException)
            {
                // e-conomic is authoritative: the candidate is taken, try the next block.
            }
        }

        throw new InvalidOperationException(
            $"Could not allocate a main project number after {MaxAttempts} attempts.");
    }

    public async Task<int> CreateSubProjectAsync(
        int mainProjectNumber,
        int preferredOffset,
        string name,
        int projectGroupNumber,
        int? customerNumber,
        int? responsibleEmployeeNumber,
        CancellationToken cancellationToken = default)
    {
        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            var offset = preferredOffset + attempt;
            if (offset > MaxSubProjectOffset)
                throw new InvalidOperationException(
                    $"No more sub-project numbers available in the block for main project {mainProjectNumber}.");

            var candidate = mainProjectNumber + offset;

            try
            {
                await _writeClient.CreateProjectAsync(
                    projectNumber: candidate,
                    name: name,
                    projectGroupNumber: projectGroupNumber,
                    customerNumber: customerNumber,
                    responsibleEmployeeNumber: responsibleEmployeeNumber,
                    isMainProject: false,
                    mainProjectNumber: mainProjectNumber,
                    cancellationToken: cancellationToken);

                return candidate;
            }
            catch (EconomicProjectNumberConflictException)
            {
                // try the next offset within the block
            }
        }

        throw new InvalidOperationException(
            $"Could not allocate a sub-project number for main project {mainProjectNumber} after {MaxAttempts} attempts.");
    }

    private async Task<int?> GetMaxProjectNumberInRangeAsync(int rangeMin, int rangeMax, CancellationToken cancellationToken)
    {
        // e-conomic's Projects endpoint only supports cursor-based pagination (see
        // EconomicProjectSourceClient, the pattern proven by the periodic sync job) —
        // it does not reliably honor server-side filter/sort, so the max number in
        // range must be determined client-side after a full fetch.
        var all = await _sourceClient.GetProjectsAsync(cancellationToken);

        int? max = null;
        foreach (var project in all)
        {
            if (project.Number >= rangeMin && project.Number <= rangeMax && (max is null || project.Number > max))
                max = project.Number;
        }

        return max;
    }
}
