using Vita.Atlas.Application.DTOs;

namespace Vita.Atlas.Application.Interfaces;

public interface IResourcePlanEntryService
{
    Task<IReadOnlyList<ResourcePlanEntryDto>> GetAllAsync(
        int? yearNumber = null,
        DateOnly? fromDate = null,
        DateOnly? toDate = null,
        int? employeeId = null,
        int? scenarioId = null,
        int? planningTargetId = null,
        int? resourcePlanId = null,
        int? virtualResourceId = null,
        IReadOnlyList<int>? employeeIds = null,
        IReadOnlyList<int>? planningTargetIds = null,
        CancellationToken cancellationToken = default);
    // Company-wide totals, pre-summed in SQL — see ResourcePlanEntrySummaryDto.
    Task<IReadOnlyList<ResourcePlanEntrySummaryDto>> GetSummaryAsync(
        int scenarioId,
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken cancellationToken = default);
    Task<ResourcePlanEntryDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<ResourcePlanEntryDto> CreateAsync(
        CreateResourcePlanEntryRequest request,
        CallerInfo caller,
        CancellationToken cancellationToken = default);
    Task<ResourcePlanEntryDto?> UpdateAsync(
        int id,
        UpdateResourcePlanEntryRequest request,
        CallerInfo caller,
        CancellationToken cancellationToken = default);
    Task<BulkUpsertResourcePlanEntriesResult> SavePeriodAsync(
        SaveResourcePlanEntriesPeriodRequest request,
        CallerInfo caller,
        CancellationToken cancellationToken = default);
    Task<BulkUpsertResourcePlanEntriesResult> AutoDistributeAsync(
        AutoDistributeResourcePlanEntriesRequest request,
        CallerInfo caller,
        CancellationToken cancellationToken = default);
    Task<ChangeResourcePlanEntriesActivityResult> ChangeActivityAsync(
        ChangeResourcePlanEntriesActivityRequest request,
        CallerInfo caller,
        CancellationToken cancellationToken = default);
    Task<ChangeResourcePlanEntriesTargetResult> ChangeTargetAsync(
        ChangeResourcePlanEntriesTargetRequest request,
        CallerInfo caller,
        CancellationToken cancellationToken = default);
}
