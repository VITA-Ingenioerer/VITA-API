using Vita.Planning.Application.DTOs;

namespace Vita.Planning.Application.Interfaces;

public interface IEconomicTimeEntryClient
{
    Task<IReadOnlyList<EconomicTimeEntryDto>> GetTimeEntriesAsync(
        int employeeNumber,
        DateTime fromDate,
        DateTime? toDate = null,
        CancellationToken cancellationToken = default);

    Task<EconomicTimeEntryDto?> GetTimeEntryAsync(int number, CancellationToken cancellationToken = default);

    Task<int> CreateTimeEntryAsync(CreateTimeEntryRequest request, CancellationToken cancellationToken = default);

    Task UpdateTimeEntryAsync(UpdateTimeEntryRequest request, CancellationToken cancellationToken = default);

    Task ApproveTimeEntriesAsync(IReadOnlyList<int> numbers, int? bookOn = null, CancellationToken cancellationToken = default);

    Task DeleteTimeEntryAsync(int number, CancellationToken cancellationToken = default);
}
