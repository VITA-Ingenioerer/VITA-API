using Vita.Atlas.Application.Exceptions;
using Vita.Atlas.Application.Interfaces;

namespace Vita.Atlas.Infrastructure.Services;

public sealed class EconomicProjectNumberAllocator : IEconomicProjectNumberAllocator
{
    // Main-project blocks are tried one after another on collision. Each attempt is a real
    // e-conomic create call, so this is a sanity bound, not the allocation strategy — the
    // starting point already comes from the highest number actually in use.
    private const int MaxMainProjectAttempts = 25;

    // A main project owns the block xxxxxx00–xxxxxx99: the main project itself is 00 and its
    // sub-projects are 01–99. Ninety-nine is therefore a hard ceiling, not a retry budget —
    // every offset in the block gets tried before allocation is declared impossible.
    private const int MinSubProjectOffset = 1;
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

        for (var attempt = 0; attempt < MaxMainProjectAttempts; attempt++)
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
            $"Could not allocate a main project number after {MaxMainProjectAttempts} attempts.");
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
        // Offsets are only meaningful relative to a block start. Given a sub-project number by
        // mistake, every candidate below would land outside its own main project's block and
        // quietly collide with the next project's numbers.
        if (mainProjectNumber % 100 != 0)
        {
            throw new InvalidOperationException(
                $"'{mainProjectNumber}' is not a main project number — sub-projects can only be created in a block ending in 00.");
        }

        foreach (var offset in EnumerateSubProjectOffsets(preferredOffset))
        {
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
                // e-conomic is authoritative: that number is taken, move to the next offset.
            }
        }

        throw new InvalidOperationException(
            $"All sub-project numbers {mainProjectNumber + MinSubProjectOffset}–{mainProjectNumber + MaxSubProjectOffset} " +
            $"are in use for main project {mainProjectNumber}.");
    }

    /// <summary>
    /// Every offset in the block exactly once: the preferred one first, upwards to 99, then
    /// wrapping to pick up gaps below it.
    ///
    /// Wrapping matters because callers derive the preferred offset from how many sub-projects
    /// they think exist. Delete sub-project 02 of five and the next create would ask for 06,
    /// and without the wrap the free 02 would never be offered — the block would look full
    /// long before it was.
    /// </summary>
    private static IEnumerable<int> EnumerateSubProjectOffsets(int preferredOffset)
    {
        var start = Math.Clamp(preferredOffset, MinSubProjectOffset, MaxSubProjectOffset);

        for (var offset = start; offset <= MaxSubProjectOffset; offset++)
        {
            yield return offset;
        }

        for (var offset = MinSubProjectOffset; offset < start; offset++)
        {
            yield return offset;
        }
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
