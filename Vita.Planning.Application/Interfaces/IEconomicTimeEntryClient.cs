using Vita.Planning.Application.DTOs;

namespace Vita.Planning.Application.Interfaces;

public interface IEconomicTimeEntryClient
{
    Task<IReadOnlyList<EconomicTimeEntryDto>> GetTimeEntriesAsync(
        int employeeNumber,
        DateTime fromDate,
        DateTime? toDate = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists time entries booked against a project across all employees (e.g. an
    /// absence/"Fravær" project), optionally narrowed to one employee.
    /// </summary>
    Task<IReadOnlyList<EconomicTimeEntryDto>> GetTimeEntriesByProjectAsync(
        int projectNumber,
        DateTime fromDate,
        DateTime? toDate = null,
        int? employeeNumber = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists time entries across all employees/projects. Pass null to fetch every entry
    /// (full backfill); pass a timestamp to fetch only entries changed since then
    /// (incremental sync, via the lastUpdated filter).
    /// </summary>
    Task<IReadOnlyList<EconomicTimeEntryDto>> GetTimeEntriesUpdatedSinceAsync(
        DateTime? updatedSinceUtc,
        CancellationToken cancellationToken = default);

    Task<EconomicTimeEntryDto?> GetTimeEntryAsync(int number, CancellationToken cancellationToken = default);

    Task<int> CreateTimeEntryAsync(CreateTimeEntryRequest request, CancellationToken cancellationToken = default);

    Task UpdateTimeEntryAsync(UpdateTimeEntryRequest request, CancellationToken cancellationToken = default);

    Task ApproveTimeEntriesAsync(IReadOnlyList<int> numbers, int? bookOn = null, CancellationToken cancellationToken = default);

    Task DeleteTimeEntryAsync(int number, CancellationToken cancellationToken = default);
}
