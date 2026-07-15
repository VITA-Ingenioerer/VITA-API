using Microsoft.EntityFrameworkCore;
using Vita.Planning.Application.DTOs;
using Vita.Planning.Application.Interfaces;
using Vita.Planning.Infrastructure.Data;
using Vita.Planning.Infrastructure.Data.Entities;

namespace Vita.Planning.Infrastructure.Services;

public sealed class ResourcePlanEntryHistoryService : IResourcePlanEntryHistoryService
{
    private readonly PlanningDbContext _dbContext;
    private readonly ICorrelationContext _correlationContext;

    public ResourcePlanEntryHistoryService(PlanningDbContext dbContext, ICorrelationContext correlationContext)
    {
        _dbContext = dbContext;
        _correlationContext = correlationContext;
    }

    public async Task<ResourcePlanEntryHistoryDto> RecordAsync(
        RecordResourcePlanEntryHistoryRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = new ResourcePlanEntryHistory
        {
            ResourcePlanEntryId = request.ResourcePlanEntryId,
            ResourcePlanId = request.ResourcePlanId,
            EmployeeId = request.EmployeeId,
            ScenarioId = request.ScenarioId,
            PlanningTargetId = request.PlanningTargetId,
            PlanDate = request.PlanDate,
            OldHours = request.OldHours,
            NewHours = request.NewHours,
            OldDescription = request.OldDescription,
            NewDescription = request.NewDescription,
            OldIsManualOverride = request.OldIsManualOverride,
            NewIsManualOverride = request.NewIsManualOverride,
            ChangeType = request.ChangeType,
            ChangeReason = request.ChangeReason,
            ChangedByUserId = request.ChangedByUserId,
            ChangedByName = request.ChangedByName,
            ChangedAtUtc = DateTime.UtcNow,
            SourceModule = request.SourceModule,
            CorrelationId = request.CorrelationId ?? _correlationContext.CorrelationId,
            MetadataJson = request.MetadataJson
        };

        _dbContext.ResourcePlanEntryHistories.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapToDto(entity);
    }

    public async Task<IReadOnlyList<ResourcePlanEntryHistoryDto>> GetByEntryIdAsync(
        int resourcePlanEntryId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.ResourcePlanEntryHistories
            .AsNoTracking()
            .Where(x => x.ResourcePlanEntryId == resourcePlanEntryId)
            .OrderByDescending(x => x.ChangedAtUtc)
            .Select(x => new ResourcePlanEntryHistoryDto
            {
                ResourcePlanEntryHistoryId = x.ResourcePlanEntryHistoryId,
                ResourcePlanEntryId = x.ResourcePlanEntryId,
                ResourcePlanId = x.ResourcePlanId,
                EmployeeId = x.EmployeeId,
                ScenarioId = x.ScenarioId,
                PlanningTargetId = x.PlanningTargetId,
                PlanDate = x.PlanDate,
                OldHours = x.OldHours,
                NewHours = x.NewHours,
                OldDescription = x.OldDescription,
                NewDescription = x.NewDescription,
                OldIsManualOverride = x.OldIsManualOverride,
                NewIsManualOverride = x.NewIsManualOverride,
                ChangeType = x.ChangeType,
                ChangeReason = x.ChangeReason,
                ChangedByUserId = x.ChangedByUserId,
                ChangedByName = x.ChangedByName,
                ChangedAtUtc = x.ChangedAtUtc,
                SourceModule = x.SourceModule,
                CorrelationId = x.CorrelationId,
                MetadataJson = x.MetadataJson
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ResourcePlanEntryHistoryDto>> GetByEmployeeAsync(
        int employeeId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.ResourcePlanEntryHistories
            .AsNoTracking()
            .Where(x => x.EmployeeId == employeeId && x.PlanDate >= from && x.PlanDate <= to)
            .OrderByDescending(x => x.ChangedAtUtc)
            .Select(x => new ResourcePlanEntryHistoryDto
            {
                ResourcePlanEntryHistoryId = x.ResourcePlanEntryHistoryId,
                ResourcePlanEntryId = x.ResourcePlanEntryId,
                ResourcePlanId = x.ResourcePlanId,
                EmployeeId = x.EmployeeId,
                ScenarioId = x.ScenarioId,
                PlanningTargetId = x.PlanningTargetId,
                PlanDate = x.PlanDate,
                OldHours = x.OldHours,
                NewHours = x.NewHours,
                OldDescription = x.OldDescription,
                NewDescription = x.NewDescription,
                OldIsManualOverride = x.OldIsManualOverride,
                NewIsManualOverride = x.NewIsManualOverride,
                ChangeType = x.ChangeType,
                ChangeReason = x.ChangeReason,
                ChangedByUserId = x.ChangedByUserId,
                ChangedByName = x.ChangedByName,
                ChangedAtUtc = x.ChangedAtUtc,
                SourceModule = x.SourceModule,
                CorrelationId = x.CorrelationId,
                MetadataJson = x.MetadataJson
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ResourcePlanEntryHistoryDto>> GetByPlanningTargetAsync(
        int planningTargetId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.ResourcePlanEntryHistories
            .AsNoTracking()
            .Where(x => x.PlanningTargetId == planningTargetId && x.PlanDate >= from && x.PlanDate <= to)
            .OrderByDescending(x => x.ChangedAtUtc)
            .Select(x => new ResourcePlanEntryHistoryDto
            {
                ResourcePlanEntryHistoryId = x.ResourcePlanEntryHistoryId,
                ResourcePlanEntryId = x.ResourcePlanEntryId,
                ResourcePlanId = x.ResourcePlanId,
                EmployeeId = x.EmployeeId,
                ScenarioId = x.ScenarioId,
                PlanningTargetId = x.PlanningTargetId,
                PlanDate = x.PlanDate,
                OldHours = x.OldHours,
                NewHours = x.NewHours,
                OldDescription = x.OldDescription,
                NewDescription = x.NewDescription,
                OldIsManualOverride = x.OldIsManualOverride,
                NewIsManualOverride = x.NewIsManualOverride,
                ChangeType = x.ChangeType,
                ChangeReason = x.ChangeReason,
                ChangedByUserId = x.ChangedByUserId,
                ChangedByName = x.ChangedByName,
                ChangedAtUtc = x.ChangedAtUtc,
                SourceModule = x.SourceModule,
                CorrelationId = x.CorrelationId,
                MetadataJson = x.MetadataJson
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ResourcePlanEntryHistoryDto>> GetByChangedByAsync(
        string changedByUserId,
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        int take = 200,
        CancellationToken cancellationToken = default)
    {
        take = take < 1 ? 200 : Math.Min(take, 1000);

        var query = _dbContext.ResourcePlanEntryHistories
            .AsNoTracking()
            .Where(x => x.ChangedByUserId == changedByUserId);

        if (fromUtc.HasValue)
        {
            query = query.Where(x => x.ChangedAtUtc >= fromUtc.Value);
        }

        if (toUtc.HasValue)
        {
            query = query.Where(x => x.ChangedAtUtc <= toUtc.Value);
        }

        return await query
            .OrderByDescending(x => x.ChangedAtUtc)
            .Take(take)
            .Select(x => new ResourcePlanEntryHistoryDto
            {
                ResourcePlanEntryHistoryId = x.ResourcePlanEntryHistoryId,
                ResourcePlanEntryId = x.ResourcePlanEntryId,
                ResourcePlanId = x.ResourcePlanId,
                EmployeeId = x.EmployeeId,
                ScenarioId = x.ScenarioId,
                PlanningTargetId = x.PlanningTargetId,
                PlanDate = x.PlanDate,
                OldHours = x.OldHours,
                NewHours = x.NewHours,
                OldDescription = x.OldDescription,
                NewDescription = x.NewDescription,
                OldIsManualOverride = x.OldIsManualOverride,
                NewIsManualOverride = x.NewIsManualOverride,
                ChangeType = x.ChangeType,
                ChangeReason = x.ChangeReason,
                ChangedByUserId = x.ChangedByUserId,
                ChangedByName = x.ChangedByName,
                ChangedAtUtc = x.ChangedAtUtc,
                SourceModule = x.SourceModule,
                CorrelationId = x.CorrelationId,
                MetadataJson = x.MetadataJson
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ResourcePlanEntryHistoryDto>> GetByCorrelationIdAsync(
        Guid correlationId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.ResourcePlanEntryHistories
            .AsNoTracking()
            .Where(x => x.CorrelationId == correlationId)
            .OrderBy(x => x.ChangedAtUtc)
            .Select(x => new ResourcePlanEntryHistoryDto
            {
                ResourcePlanEntryHistoryId = x.ResourcePlanEntryHistoryId,
                ResourcePlanEntryId = x.ResourcePlanEntryId,
                ResourcePlanId = x.ResourcePlanId,
                EmployeeId = x.EmployeeId,
                ScenarioId = x.ScenarioId,
                PlanningTargetId = x.PlanningTargetId,
                PlanDate = x.PlanDate,
                OldHours = x.OldHours,
                NewHours = x.NewHours,
                OldDescription = x.OldDescription,
                NewDescription = x.NewDescription,
                OldIsManualOverride = x.OldIsManualOverride,
                NewIsManualOverride = x.NewIsManualOverride,
                ChangeType = x.ChangeType,
                ChangeReason = x.ChangeReason,
                ChangedByUserId = x.ChangedByUserId,
                ChangedByName = x.ChangedByName,
                ChangedAtUtc = x.ChangedAtUtc,
                SourceModule = x.SourceModule,
                CorrelationId = x.CorrelationId,
                MetadataJson = x.MetadataJson
            })
            .ToListAsync(cancellationToken);
    }

    private static ResourcePlanEntryHistoryDto MapToDto(ResourcePlanEntryHistory x) => new()
    {
        ResourcePlanEntryHistoryId = x.ResourcePlanEntryHistoryId,
        ResourcePlanEntryId = x.ResourcePlanEntryId,
        ResourcePlanId = x.ResourcePlanId,
        EmployeeId = x.EmployeeId,
        ScenarioId = x.ScenarioId,
        PlanningTargetId = x.PlanningTargetId,
        PlanDate = x.PlanDate,
        OldHours = x.OldHours,
        NewHours = x.NewHours,
        OldDescription = x.OldDescription,
        NewDescription = x.NewDescription,
        OldIsManualOverride = x.OldIsManualOverride,
        NewIsManualOverride = x.NewIsManualOverride,
        ChangeType = x.ChangeType,
        ChangeReason = x.ChangeReason,
        ChangedByUserId = x.ChangedByUserId,
        ChangedByName = x.ChangedByName,
        ChangedAtUtc = x.ChangedAtUtc,
        SourceModule = x.SourceModule,
        CorrelationId = x.CorrelationId,
        MetadataJson = x.MetadataJson
    };
}

public sealed class BusinessEventService : IBusinessEventService
{
    private readonly PlanningDbContext _dbContext;
    private readonly ICorrelationContext _correlationContext;

    public BusinessEventService(PlanningDbContext dbContext, ICorrelationContext correlationContext)
    {
        _dbContext = dbContext;
        _correlationContext = correlationContext;
    }

    public async Task<BusinessEventDto> RecordAsync(
        RecordBusinessEventRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = new BusinessEvent
        {
            EventType = request.EventType,
            EventTitle = request.EventTitle,
            EventDescription = request.EventDescription,
            EntityType = request.EntityType,
            EntityId = request.EntityId,
            PlanningTargetId = request.PlanningTargetId,
            OldValue = request.OldValue,
            NewValue = request.NewValue,
            PayloadJson = request.PayloadJson,
            CreatedByUserId = request.CreatedByUserId,
            CreatedByName = request.CreatedByName,
            CreatedAtUtc = DateTime.UtcNow,
            SourceModule = request.SourceModule,
            CorrelationId = request.CorrelationId ?? _correlationContext.CorrelationId
        };

        _dbContext.BusinessEvents.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapToDto(entity);
    }

    public async Task<PagedResultDto<BusinessEventDto>> QueryAsync(
        string? entityType = null,
        string? entityId = null,
        int? planningTargetId = null,
        string? eventType = null,
        string? createdByUserId = null,
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize < 1 ? 50 : Math.Min(pageSize, 200);

        var query = _dbContext.BusinessEvents.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(entityType))
        {
            var normalizedEntityType = entityType.Trim();
            query = query.Where(x => x.EntityType == normalizedEntityType);
        }

        if (!string.IsNullOrWhiteSpace(createdByUserId))
        {
            var normalizedCreatedByUserId = createdByUserId.Trim();
            query = query.Where(x => x.CreatedByUserId == normalizedCreatedByUserId);
        }

        if (!string.IsNullOrWhiteSpace(entityId))
        {
            var normalizedEntityId = entityId.Trim();
            query = query.Where(x => x.EntityId == normalizedEntityId);
        }

        if (planningTargetId.HasValue)
        {
            query = query.Where(x => x.PlanningTargetId == planningTargetId.Value);
        }

        if (!string.IsNullOrWhiteSpace(eventType))
        {
            var normalizedEventType = eventType.Trim();
            query = query.Where(x => x.EventType == normalizedEventType);
        }

        if (fromUtc.HasValue)
        {
            query = query.Where(x => x.CreatedAtUtc >= fromUtc.Value);
        }

        if (toUtc.HasValue)
        {
            query = query.Where(x => x.CreatedAtUtc <= toUtc.Value);
        }

        query = query.OrderByDescending(x => x.CreatedAtUtc).ThenByDescending(x => x.BusinessEventId);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => MapToDtoExpression(x))
            .ToListAsync(cancellationToken);

        return new PagedResultDto<BusinessEventDto>
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize),
            Items = items
        };
    }

    public async Task<IReadOnlyList<BusinessEventDto>> GetByEntityAsync(
        string entityType,
        string entityId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.BusinessEvents
            .AsNoTracking()
            .Where(x => x.EntityType == entityType && x.EntityId == entityId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => MapToDtoExpression(x))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<BusinessEventDto>> GetByPlanningTargetAsync(
        int planningTargetId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.BusinessEvents
            .AsNoTracking()
            .Where(x => x.PlanningTargetId == planningTargetId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => MapToDtoExpression(x))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<BusinessEventDto>> GetByCorrelationIdAsync(
        Guid correlationId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.BusinessEvents
            .AsNoTracking()
            .Where(x => x.CorrelationId == correlationId)
            .OrderBy(x => x.CreatedAtUtc)
            .Select(x => MapToDtoExpression(x))
            .ToListAsync(cancellationToken);
    }

    private static BusinessEventDto MapToDtoExpression(BusinessEvent x) => new()
    {
        BusinessEventId = x.BusinessEventId,
        EventType = x.EventType,
        EventTitle = x.EventTitle,
        EventDescription = x.EventDescription,
        EntityType = x.EntityType,
        EntityId = x.EntityId,
        PlanningTargetId = x.PlanningTargetId,
        OldValue = x.OldValue,
        NewValue = x.NewValue,
        PayloadJson = x.PayloadJson,
        CreatedByUserId = x.CreatedByUserId,
        CreatedByName = x.CreatedByName,
        CreatedAtUtc = x.CreatedAtUtc,
        SourceModule = x.SourceModule,
        CorrelationId = x.CorrelationId
    };

    private static BusinessEventDto MapToDto(BusinessEvent x) => MapToDtoExpression(x);
}
