using Vita.Planning.Application.DTOs;

namespace Vita.Planning.Application.Interfaces;

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
}
