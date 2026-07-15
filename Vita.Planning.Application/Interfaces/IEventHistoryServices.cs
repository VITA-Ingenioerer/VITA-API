using Vita.Planning.Application.DTOs;

namespace Vita.Planning.Application.Interfaces;

public interface IResourcePlanEntryHistoryService
{
    Task<ResourcePlanEntryHistoryDto> RecordAsync(RecordResourcePlanEntryHistoryRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ResourcePlanEntryHistoryDto>> GetByEntryIdAsync(int resourcePlanEntryId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ResourcePlanEntryHistoryDto>> GetByEmployeeAsync(int employeeId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ResourcePlanEntryHistoryDto>> GetByPlanningTargetAsync(int planningTargetId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ResourcePlanEntryHistoryDto>> GetByCorrelationIdAsync(Guid correlationId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ResourcePlanEntryHistoryDto>> GetByChangedByAsync(
        string changedByUserId,
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        int take = 200,
        CancellationToken cancellationToken = default);
}

public interface IBusinessEventService
{
    Task<BusinessEventDto> RecordAsync(RecordBusinessEventRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BusinessEventDto>> QueryAsync(
        string? entityType = null,
        string? entityId = null,
        int? planningTargetId = null,
        string? eventType = null,
        string? createdByUserId = null,
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        int take = 200,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BusinessEventDto>> GetByEntityAsync(string entityType, string entityId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BusinessEventDto>> GetByPlanningTargetAsync(int planningTargetId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BusinessEventDto>> GetByCorrelationIdAsync(Guid correlationId, CancellationToken cancellationToken = default);
}
