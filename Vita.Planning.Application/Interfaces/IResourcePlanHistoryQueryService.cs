using Vita.Planning.Application.DTOs;

namespace Vita.Planning.Application.Interfaces;

/// <summary>
/// Read-side queries over resource_plan_entry_history: a human-readable change logbook,
/// and point-in-time reconstruction/comparison of planned hours ("what did this plan look
/// like as of date X", "what changed between date X and date Y").
/// </summary>
public interface IResourcePlanHistoryQueryService
{
    Task<PagedResultDto<ResourcePlanLogbookEntryDto>> GetLogbookAsync(
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        int? employeeId = null,
        int? planningTargetId = null,
        string? changedByUserId = null,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ResourcePlanAsOfEntryDto>> GetStateAsOfAsync(
        DateTime asOfUtc,
        DateOnly periodFrom,
        DateOnly periodTo,
        int? employeeId = null,
        int? scenarioId = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ResourcePlanAsOfComparisonEntryDto>> CompareAsOfAsync(
        DateTime asOfFromUtc,
        DateTime asOfToUtc,
        DateOnly periodFrom,
        DateOnly periodTo,
        int? employeeId = null,
        int? scenarioId = null,
        CancellationToken cancellationToken = default);
}
