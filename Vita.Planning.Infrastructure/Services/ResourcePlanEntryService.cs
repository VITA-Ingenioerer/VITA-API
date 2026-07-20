using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Linq.Expressions;
using Vita.Planning.Application.DTOs;
using Vita.Planning.Application.Interfaces;
using Vita.Planning.Infrastructure.Data;
using Vita.Planning.Infrastructure.Data.Entities;

namespace Vita.Planning.Infrastructure.Services;

public sealed class ResourcePlanEntryService : IResourcePlanEntryService
{
    // Beyond this, an unscoped list query joins/sorts too much of a fast-growing table;
    // callers must narrow by employee/scenario/plan/target instead.
    private const int MaxUnscopedDateRangeDays = 120;
    private static readonly TimeSpan ReferenceCacheDuration = TimeSpan.FromMinutes(5);
    private const string PlanningTargetLookupCacheKey = "ResourcePlanEntryService:PlanningTargetLookup";
    private const string ActivityLookupCacheKey = "ResourcePlanEntryService:ActivityLookup";

    private readonly PlanningDbContext _dbContext;
    private readonly IResourcePlanEntryHistoryService _historyService;
    private readonly IEntityChangeLogService _changeLogService;
    private readonly ICapacityScheduleQueryService _capacityScheduleQueryService;
    private readonly IMemoryCache _cache;
    private readonly ICorrelationContext _correlationContext;

    public ResourcePlanEntryService(
        PlanningDbContext dbContext,
        IResourcePlanEntryHistoryService historyService,
        IEntityChangeLogService changeLogService,
        ICapacityScheduleQueryService capacityScheduleQueryService,
        IMemoryCache cache,
        ICorrelationContext correlationContext)
    {
        _dbContext = dbContext;
        _historyService = historyService;
        _changeLogService = changeLogService;
        _capacityScheduleQueryService = capacityScheduleQueryService;
        _cache = cache;
        _correlationContext = correlationContext;
    }

    private sealed record PlanningTargetLookup(
        string Code,
        string Name,
        string TargetType,
        int? ExtProjectNumber,
        int? OfferId,
        int? InternalPlanningCodeId);

    private sealed record ActivityLookup(int ActivityNumber, string Name);

    private Task<Dictionary<int, PlanningTargetLookup>> GetPlanningTargetLookupAsync(CancellationToken cancellationToken)
    {
        return _cache.GetOrCreateAsync(PlanningTargetLookupCacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = ReferenceCacheDuration;
            return await _dbContext.PlanningTargets
                .AsNoTracking()
                .Select(t => new
                {
                    t.PlanningTargetId,
                    t.Code,
                    t.Name,
                    t.TargetType,
                    t.ExtProjectNumber,
                    t.OfferId,
                    t.InternalPlanningCodeId
                })
                .ToDictionaryAsync(
                    t => t.PlanningTargetId,
                    t => new PlanningTargetLookup(t.Code, t.Name, t.TargetType, t.ExtProjectNumber, t.OfferId, t.InternalPlanningCodeId),
                    cancellationToken);
        })!;
    }

    private Task<Dictionary<int, ActivityLookup>> GetActivityLookupAsync(CancellationToken cancellationToken)
    {
        return _cache.GetOrCreateAsync(ActivityLookupCacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = ReferenceCacheDuration;
            return await (
                from projectActivity in _dbContext.ProjectActivities.AsNoTracking()
                join activity in _dbContext.Activities.AsNoTracking()
                    on projectActivity.ActivityNumber equals activity.ActivityNumber
                select new { projectActivity.Number, activity.ActivityNumber, activity.Name })
                .ToDictionaryAsync(
                    x => x.Number,
                    x => new ActivityLookup(x.ActivityNumber, x.Name),
                    cancellationToken);
        })!;
    }

    public async Task<IReadOnlyList<ResourcePlanEntryDto>> GetAllAsync(
        int? yearNumber = null,
        DateOnly? fromDate = null,
        DateOnly? toDate = null,
        int? employeeId = null,
        int? scenarioId = null,
        int? planningTargetId = null,
        int? resourcePlanId = null,
        int? virtualResourceId = null,
        CancellationToken cancellationToken = default)
    {
        var hasNarrowingScope = employeeId.HasValue || scenarioId.HasValue || resourcePlanId.HasValue
            || planningTargetId.HasValue || virtualResourceId.HasValue;

        if (!hasNarrowingScope && fromDate.HasValue && toDate.HasValue
            && toDate.Value.DayNumber - fromDate.Value.DayNumber > MaxUnscopedDateRangeDays)
        {
            throw new InvalidOperationException(
                $"Date range spans more than {MaxUnscopedDateRangeDays} days; narrow it with employeeId, virtualResourceId, scenarioId, resourcePlanId or planningTargetId.");
        }

        var query = _dbContext.ResourcePlanEntries.AsNoTracking();

        if (yearNumber.HasValue)
        {
            query = query.Where(x => x.PlanDate.Year == yearNumber.Value);
        }

        if (fromDate.HasValue)
        {
            query = query.Where(x => x.PlanDate >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            query = query.Where(x => x.PlanDate <= toDate.Value);
        }

        if (employeeId.HasValue)
        {
            query = query.Where(x => x.ResourcePlan != null && x.ResourcePlan.EmployeeId == employeeId.Value);
        }

        if (virtualResourceId.HasValue)
        {
            query = query.Where(x => x.ResourcePlan != null && x.ResourcePlan.VirtualResourceId == virtualResourceId.Value);
        }

        if (scenarioId.HasValue)
        {
            query = query.Where(x => x.ResourcePlan != null && x.ResourcePlan.ScenarioId == scenarioId.Value);
        }

        if (planningTargetId.HasValue)
        {
            query = query.Where(x => x.PlanningTargetId == planningTargetId.Value);
        }

        if (resourcePlanId.HasValue)
        {
            query = query.Where(x => x.ResourcePlanId == resourcePlanId.Value);
        }

        // Only join the fact table (resource_plan_entries -> resource_plans); planning target and
        // activity labels come from the cached lookups below instead of per-row SQL joins, since
        // those reference tables grow with project/activity count, not with entry count.
        var rows = await query
            .OrderBy(x => x.PlanDate)
            .ThenBy(x => x.ResourcePlanEntryId)
            .Select(x => new
            {
                x.ResourcePlanEntryId,
                x.ResourcePlanId,
                EmployeeId = x.ResourcePlan != null ? x.ResourcePlan.EmployeeId : null,
                VirtualResourceId = x.ResourcePlan != null ? x.ResourcePlan.VirtualResourceId : null,
                ScenarioId = x.ResourcePlan != null ? x.ResourcePlan.ScenarioId : 0,
                x.Hours,
                x.Description,
                x.IsManualOverride,
                x.CreatedBy,
                x.UpdatedBy,
                x.CreatedAt,
                x.UpdatedAt,
                x.PlanningTargetId,
                x.PlanDate,
                x.ProjectActivityId
            })
            .ToListAsync(cancellationToken);

        var planningTargetLookup = await GetPlanningTargetLookupAsync(cancellationToken);
        var activityLookup = await GetActivityLookupAsync(cancellationToken);

        return rows.ConvertAll(r =>
        {
            planningTargetLookup.TryGetValue(r.PlanningTargetId, out var planningTarget);
            ActivityLookup? activity = r.ProjectActivityId.HasValue
                ? activityLookup.GetValueOrDefault(r.ProjectActivityId.Value)
                : null;

            return new ResourcePlanEntryDto
            {
                ResourcePlanEntryId = r.ResourcePlanEntryId,
                ResourcePlanId = r.ResourcePlanId,
                EmployeeId = r.EmployeeId,
                VirtualResourceId = r.VirtualResourceId,
                ScenarioId = r.ScenarioId,
                Hours = r.Hours,
                Description = r.Description,
                IsManualOverride = r.IsManualOverride,
                CreatedBy = r.CreatedBy,
                UpdatedBy = r.UpdatedBy,
                CreatedAt = r.CreatedAt,
                UpdatedAt = r.UpdatedAt,
                PlanningTargetId = r.PlanningTargetId,
                PlanningTargetCode = planningTarget?.Code,
                PlanningTargetName = planningTarget?.Name,
                PlanningTargetType = planningTarget?.TargetType,
                ProjectNumber = planningTarget?.ExtProjectNumber,
                OfferId = planningTarget?.OfferId,
                InternalPlanningCodeId = planningTarget?.InternalPlanningCodeId,
                PlanDate = r.PlanDate,
                ProjectActivityId = r.ProjectActivityId,
                ActivityNumber = activity?.ActivityNumber,
                ActivityName = activity?.Name
            };
        });
    }

    public async Task<ResourcePlanEntryDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.ResourcePlanEntries
            .AsNoTracking()
            .Where(x => x.ResourcePlanEntryId == id)
            .Select(MapToDtoExpression())
            .FirstOrDefaultAsync(cancellationToken);
    }
    private async Task<ResourcePlan> RequireResourcePlanAsync(
    int resourcePlanId,
    CancellationToken cancellationToken)
    {
        var resourcePlan = await _dbContext.ResourcePlans
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.ResourcePlanId == resourcePlanId, cancellationToken);

        if (resourcePlan is null)
        {
            throw new KeyNotFoundException($"Resource plan '{resourcePlanId}' was not found.");
        }

        return resourcePlan;
    }

    private async Task RequirePlanningTargetAsync(
        int planningTargetId,
        CancellationToken cancellationToken)
    {
        var exists = await _dbContext.PlanningTargets
            .AsNoTracking()
            .AnyAsync(x =>
                x.PlanningTargetId == planningTargetId &&
                x.IsActive &&
                x.IsPlannable,
                cancellationToken);

        if (!exists)
        {
            throw new KeyNotFoundException($"Planning target '{planningTargetId}' was not found or is not plannable.");
        }
    }

    // Enforces that an activity can only be attached to an entry if it's actually assigned to
    // that entry's project in e-conomic (ext.project_activities) — never a global, unscoped pick.
    private async Task RequireValidProjectActivityAsync(
        int? projectActivityId,
        int planningTargetId,
        CancellationToken cancellationToken)
    {
        if (!projectActivityId.HasValue)
        {
            return;
        }

        var projectNumber = await _dbContext.PlanningTargets
            .AsNoTracking()
            .Where(t => t.PlanningTargetId == planningTargetId)
            .Select(t => t.ExtProjectNumber)
            .FirstOrDefaultAsync(cancellationToken);

        var isValidForProject = projectNumber.HasValue && await _dbContext.ProjectActivities
            .AsNoTracking()
            .AnyAsync(x =>
                x.Number == projectActivityId.Value &&
                x.ProjectNumber == projectNumber.Value,
                cancellationToken);

        if (!isValidForProject)
        {
            throw new InvalidOperationException(
                $"Activity '{projectActivityId.Value}' is not assigned to this project and cannot be used here.");
        }
    }

    public async Task<ResourcePlanEntryDto> CreateAsync(
        CreateResourcePlanEntryRequest request,
        CallerInfo caller,
        CancellationToken cancellationToken = default)
    {
        ValidatePlanDate(request.PlanDate);
        ValidateHours(request.Hours);

        var duplicate = await BuildDuplicateQuery(
                request.ResourcePlanId,
                request.PlanningTargetId,
                request.PlanDate)
            .AnyAsync(cancellationToken);

        if (duplicate)
        {
            throw new InvalidOperationException("An entry for this plan date already exists.");
        }

        var strategy = _dbContext.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

            var resourcePlan = await RequireResourcePlanAsync(request.ResourcePlanId, cancellationToken);
            await RequirePlanningTargetAsync(request.PlanningTargetId, cancellationToken);
            await RequireValidProjectActivityAsync(request.ProjectActivityId, request.PlanningTargetId, cancellationToken);

            var entity = new ResourcePlanEntry
            {
                ResourcePlanId = request.ResourcePlanId,
                Hours = request.Hours,
                Description = NormalizeNullable(request.Description),
                IsManualOverride = request.IsManualOverride,
                CreatedBy = ResolveActor(caller),
                PlanningTargetId = request.PlanningTargetId,
                ProjectActivityId = request.ProjectActivityId,
                PlanDate = request.PlanDate,
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.ResourcePlanEntries.Add(entity);
            await _dbContext.SaveChangesAsync(cancellationToken);

            var correlationId = _correlationContext.CorrelationId;
            await _historyService.RecordAsync(
                BuildHistoryRecord(
                    entity,
                    resourcePlan,
                    null,
                    entity.Hours,
                    null,
                    entity.Description,
                    null,
                    entity.IsManualOverride,
                    "Created",
                    "Created",
                    caller,
                    correlationId),
                cancellationToken);

            await _changeLogService.RecordChangeAsync(new RecordEntityChangeRequest
            {
                EventType = "ResourcePlanEntryCreated",
                EventTitle = $"Ressourceplan entry oprettet: {entity.PlanDate:yyyy-MM-dd}",
                EntityType = "ResourcePlanEntry",
                EntityId = entity.ResourcePlanEntryId.ToString(),
                PlanningTargetId = entity.PlanningTargetId,
                NewValue = $"{entity.Hours:0.##} timer",
                NewSnapshot = BuildEntrySnapshot(entity, resourcePlan),
                ChangeReason = "Created",
                Caller = caller,
                SourceModule = "ResourcePlanEntryService",
                CorrelationId = correlationId
            }, cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            return await MapToDtoWithActivityAsync(entity, resourcePlan, cancellationToken);
        });
    }

    public async Task<ResourcePlanEntryDto?> UpdateAsync(
        int id,
        UpdateResourcePlanEntryRequest request,
        CallerInfo caller,
        CancellationToken cancellationToken = default)
    {
        ValidatePlanDate(request.PlanDate);
        ValidateHours(request.Hours);

        var entity = await _dbContext.ResourcePlanEntries
            .FirstOrDefaultAsync(x => x.ResourcePlanEntryId == id, cancellationToken);

        if (entity is null)
        {
            return null;
        }

        var duplicate = await BuildDuplicateQuery(
                entity.ResourcePlanId,
                request.PlanningTargetId,
                request.PlanDate)
            .AnyAsync(e => e.ResourcePlanEntryId != id, cancellationToken);

        if (duplicate)
        {
            throw new InvalidOperationException("An entry for this plan date already exists.");
        }

        var strategy = _dbContext.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

            var resourcePlan = await RequireResourcePlanAsync(entity.ResourcePlanId, cancellationToken);
            await RequirePlanningTargetAsync(request.PlanningTargetId, cancellationToken);
            await RequireValidProjectActivityAsync(request.ProjectActivityId, request.PlanningTargetId, cancellationToken);

            var oldSnapshot = BuildEntrySnapshot(entity, resourcePlan);
            var oldHours = entity.Hours;
            var oldDescription = entity.Description;
            var oldIsManualOverride = entity.IsManualOverride;

            entity.Hours = request.Hours;
            entity.Description = NormalizeNullable(request.Description);
            entity.IsManualOverride = request.IsManualOverride;
            entity.UpdatedBy = ResolveActor(caller);
            entity.PlanningTargetId = request.PlanningTargetId;
            entity.ProjectActivityId = request.ProjectActivityId;
            entity.PlanDate = request.PlanDate;
            entity.UpdatedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync(cancellationToken);

            var correlationId = _correlationContext.CorrelationId;
            await _historyService.RecordAsync(
                BuildHistoryRecord(
                    entity,
                    resourcePlan,
                    oldHours,
                    entity.Hours,
                    oldDescription,
                    entity.Description,
                    oldIsManualOverride,
                    entity.IsManualOverride,
                    "Updated",
                    "Updated",
                    caller,
                    correlationId),
                cancellationToken);

            await _changeLogService.RecordChangeAsync(new RecordEntityChangeRequest
            {
                EventType = "ResourcePlanEntryUpdated",
                EventTitle = $"Ressourceplan entry ændret: {entity.PlanDate:yyyy-MM-dd}",
                EntityType = "ResourcePlanEntry",
                EntityId = entity.ResourcePlanEntryId.ToString(),
                PlanningTargetId = entity.PlanningTargetId,
                OldValue = $"{oldHours:0.##} timer",
                NewValue = $"{entity.Hours:0.##} timer",
                OldSnapshot = oldSnapshot,
                NewSnapshot = BuildEntrySnapshot(entity, resourcePlan),
                ChangeReason = "Updated",
                Caller = caller,
                SourceModule = "ResourcePlanEntryService",
                CorrelationId = correlationId
            }, cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            return await MapToDtoWithActivityAsync(entity, resourcePlan, cancellationToken);
        });
    }

    public async Task<BulkUpsertResourcePlanEntriesResult> SavePeriodAsync(
        SaveResourcePlanEntriesPeriodRequest request,
        CallerInfo caller,
        CancellationToken cancellationToken = default)
    {
        if (request.Periods.Count == 0)
        {
            throw new InvalidOperationException("At least one period is required.");
        }

        var minDate = request.Periods.Min(x => x.FromDate);
        var maxDate = request.Periods.Max(x => x.ToDate);

        var vitaHolidays = await _dbContext.Set<VitaHoliday>()
            .AsNoTracking()
            .Where(x => x.CountryCode == "DK")
            .Where(x => x.HolidayDate >= minDate && x.HolidayDate <= maxDate)
            .Where(x => x.IsActive != false)
            .ToListAsync(cancellationToken);

        var holidayLookup = vitaHolidays
            .GroupBy(x => x.HolidayDate)
            .ToDictionary(x => x.Key, x => x.ToList());

        var strategy = _dbContext.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

            var correlationId = _correlationContext.CorrelationId;
            var actor = ResolveActor(caller);

            var planningTargetIds = request.Periods.Select(x => x.PlanningTargetId).Distinct().ToList();

            var existingPlanningTargetIds = await _dbContext.PlanningTargets
                .Where(x => planningTargetIds.Contains(x.PlanningTargetId))
                .Select(x => x.PlanningTargetId)
                .ToListAsync(cancellationToken);

            var missingPlanningTargetId = planningTargetIds.Except(existingPlanningTargetIds).FirstOrDefault();
            if (missingPlanningTargetId != 0)
            {
                throw new KeyNotFoundException($"Planning target '{missingPlanningTargetId}' was not found.");
            }

            var resourcePlanIds = request.Periods.Select(x => x.ResourcePlanId).Distinct().ToList();
            var resourcePlans = await _dbContext.ResourcePlans
                .Where(x => resourcePlanIds.Contains(x.ResourcePlanId))
                .ToDictionaryAsync(x => x.ResourcePlanId, cancellationToken);

            var missingResourcePlanId = resourcePlanIds
                .Except(resourcePlans.Keys)
                .FirstOrDefault();

            if (missingResourcePlanId != 0)
            {
                throw new KeyNotFoundException($"Resource plan '{missingResourcePlanId}' was not found.");
            }

            var touchedIds = new List<int>();
            var historyRecords = new List<RecordResourcePlanEntryHistoryRequest>();

            foreach (var period in request.Periods)
            {
                ValidatePlanDate(period.FromDate);
                ValidatePlanDate(period.ToDate);
                ValidateHours(period.Hours);

                if (period.ToDate < period.FromDate)
                {
                    throw new InvalidOperationException("ToDate must be greater than or equal to FromDate.");
                }

                resourcePlans.TryGetValue(period.ResourcePlanId, out var resourcePlan);

                var employeeDailyHours = await _capacityScheduleQueryService.GetEffectiveDailyHoursAsync(
                    resourcePlan!.EmployeeId, period.FromDate, period.ToDate, cancellationToken);

                var targetHoursByDate = BuildWorkingDayDistribution(period, holidayLookup, employeeDailyHours);
                var existingEntries = await _dbContext.ResourcePlanEntries
                    .Where(x => x.ResourcePlanId == period.ResourcePlanId)
                    .Where(x => x.PlanningTargetId == period.PlanningTargetId)
                    .Where(x => x.PlanDate >= period.FromDate && x.PlanDate <= period.ToDate)
                    .OrderBy(x => x.PlanDate)
                    .ThenBy(x => x.ResourcePlanEntryId)
                    .ToListAsync(cancellationToken);

                var groupedEntries = existingEntries
                    .GroupBy(x => x.PlanDate)
                    .ToDictionary(x => x.Key, x => x.OrderBy(y => y.ResourcePlanEntryId).ToList());

                foreach (var entryGroup in groupedEntries)
                {
                    if (!targetHoursByDate.TryGetValue(entryGroup.Key, out var targetHours))
                    {
                        var entryToZero = entryGroup.Value[0];
                        var oldHours = entryToZero.Hours;
                        var oldDescription = entryToZero.Description;
                        var oldIsManualOverride = entryToZero.IsManualOverride;
                        entryToZero.Hours = 0m;
                        entryToZero.UpdatedBy = actor;
                        entryToZero.UpdatedAt = DateTime.UtcNow;
                        touchedIds.Add(entryToZero.ResourcePlanEntryId);
                        historyRecords.Add(BuildHistoryRecord(entryToZero, resourcePlan, oldHours, 0m, oldDescription, entryToZero.Description, oldIsManualOverride, entryToZero.IsManualOverride, "Updated", "ZeroedOutOfRange", caller, correlationId));
                        continue;
                    }

                    var entryToKeep = entryGroup.Value[0];
                    var oldKeepHours = entryToKeep.Hours;
                    var oldKeepDescription = entryToKeep.Description;
                    var oldKeepIsManualOverride = entryToKeep.IsManualOverride;
                    entryToKeep.Hours = targetHours;
                    entryToKeep.Description = NormalizeNullable(period.Description);
                    entryToKeep.IsManualOverride = period.IsManualOverride;
                    entryToKeep.UpdatedBy = actor;
                    entryToKeep.UpdatedAt = DateTime.UtcNow;
                    touchedIds.Add(entryToKeep.ResourcePlanEntryId);
                    historyRecords.Add(BuildHistoryRecord(entryToKeep, resourcePlan, oldKeepHours, targetHours, oldKeepDescription, entryToKeep.Description, oldKeepIsManualOverride, entryToKeep.IsManualOverride, "Updated", "SavePeriod", caller, correlationId));

                    targetHoursByDate.Remove(entryGroup.Key);
                }

                foreach (var allocation in targetHoursByDate.OrderBy(x => x.Key))
                {
                    var newEntity = new ResourcePlanEntry
                    {
                        ResourcePlanId = period.ResourcePlanId,
                        PlanningTargetId = period.PlanningTargetId,
                        PlanDate = allocation.Key,
                        Hours = allocation.Value,
                        Description = NormalizeNullable(period.Description),
                        IsManualOverride = period.IsManualOverride,
                        CreatedBy = actor,
                        CreatedAt = DateTime.UtcNow
                    };

                    _dbContext.ResourcePlanEntries.Add(newEntity);
                    await _dbContext.SaveChangesAsync(cancellationToken);
                    touchedIds.Add(newEntity.ResourcePlanEntryId);
                    historyRecords.Add(BuildHistoryRecord(newEntity, resourcePlan, null, allocation.Value, null, newEntity.Description, null, newEntity.IsManualOverride, "Created", "SavePeriod", caller, correlationId));
                }
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            foreach (var record in historyRecords)
            {
                await _historyService.RecordAsync(record, cancellationToken);
            }

            await _changeLogService.RecordChangeAsync(new RecordEntityChangeRequest
            {
                EventType = "ResourcePlanPeriodSaved",
                EventTitle = "Resource plan period saved",
                EntityType = "ResourcePlanDistribution",
                EntityId = correlationId.ToString(),
                PlanningTargetId = request.Periods.FirstOrDefault()?.PlanningTargetId,
                NewValue = $"{touchedIds.Count} entries affected",
                NewSnapshot = new
                {
                    Periods = request.Periods,
                    TouchedResourcePlanEntryIds = touchedIds,
                    HistoryRecordCount = historyRecords.Count
                },
                ChangeReason = "SavePeriod",
                Caller = caller,
                CorrelationId = correlationId,
                SourceModule = "ResourcePlanEntriesController"
            }, cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            var entries = await _dbContext.ResourcePlanEntries
                .AsNoTracking()
                .Where(x => touchedIds.Contains(x.ResourcePlanEntryId))
                .OrderBy(x => x.PlanDate)
                .ThenBy(x => x.ResourcePlanEntryId)
                .Select(MapToDtoExpression())
                .ToListAsync(cancellationToken);

            return new BulkUpsertResourcePlanEntriesResult
            {
                Entries = entries
            };
        });
    }

    public async Task<BulkUpsertResourcePlanEntriesResult> AutoDistributeAsync(
        AutoDistributeResourcePlanEntriesRequest request,
        CallerInfo caller,
        CancellationToken cancellationToken = default)
    {
        if (request.Distributions.Count == 0)
        {
            throw new InvalidOperationException("At least one distribution is required.");
        }

        var minDate = request.Distributions.Min(x => x.FromDate);
        var maxDate = request.Distributions.Max(x => x.ToDate);

        var vitaHolidays = await _dbContext.Set<VitaHoliday>()
            .AsNoTracking()
            .Where(x => x.CountryCode == "DK")
            .Where(x => x.HolidayDate >= minDate && x.HolidayDate <= maxDate)
            .Where(x => x.IsActive != false)
            .ToListAsync(cancellationToken);

        var holidayLookup = vitaHolidays
            .GroupBy(x => x.HolidayDate)
            .ToDictionary(x => x.Key, x => x.ToList());

        var strategy = _dbContext.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

            var correlationId = _correlationContext.CorrelationId;
            var actor = ResolveActor(caller);

            var planningTargetIds = request.Distributions.Select(x => x.PlanningTargetId).Distinct().ToList();

            var existingPlanningTargetIds = await _dbContext.PlanningTargets
                .Where(x => planningTargetIds.Contains(x.PlanningTargetId))
                .Select(x => x.PlanningTargetId)
                .ToListAsync(cancellationToken);

            var missingPlanningTargetId = planningTargetIds.Except(existingPlanningTargetIds).FirstOrDefault();
            if (missingPlanningTargetId != 0)
            {
                throw new KeyNotFoundException($"Planning target '{missingPlanningTargetId}' was not found.");
            }

            var resourcePlanIds = request.Distributions.Select(x => x.ResourcePlanId).Distinct().ToList();
            var resourcePlans = await _dbContext.ResourcePlans
                .Where(x => resourcePlanIds.Contains(x.ResourcePlanId))
                .ToDictionaryAsync(x => x.ResourcePlanId, cancellationToken);

            var missingResourcePlanId = resourcePlanIds
                .Except(resourcePlans.Keys)
                .FirstOrDefault();

            if (missingResourcePlanId != 0)
            {
                throw new KeyNotFoundException($"Resource plan '{missingResourcePlanId}' was not found.");
            }

            var touchedIds = new List<int>();
            var historyRecords = new List<RecordResourcePlanEntryHistoryRequest>();

            foreach (var distribution in request.Distributions)
            {
                ValidatePlanDate(distribution.FromDate);
                ValidatePlanDate(distribution.ToDate);
                ValidateHours(distribution.Hours);

                if (distribution.ToDate < distribution.FromDate)
                {
                    throw new InvalidOperationException("ToDate must be greater than or equal to FromDate.");
                }

                await RequireValidProjectActivityAsync(distribution.ProjectActivityId, distribution.PlanningTargetId, cancellationToken);

                resourcePlans.TryGetValue(distribution.ResourcePlanId, out var resourcePlan);

                var employeeDailyHours = await _capacityScheduleQueryService.GetEffectiveDailyHoursAsync(
                    resourcePlan!.EmployeeId, distribution.FromDate, distribution.ToDate, cancellationToken);

                var targetHoursByDate = BuildAutoDistribution(distribution, holidayLookup, employeeDailyHours);
                // Scoped to this distribution's own activity bucket too — without
                // this, two distributions for the same target/date range but
                // different activities (or one with, one without) would each see
                // the other's entries as "existing" and clobber them.
                var existingEntries = await _dbContext.ResourcePlanEntries
                    .Where(x => x.ResourcePlanId == distribution.ResourcePlanId)
                    .Where(x => x.PlanningTargetId == distribution.PlanningTargetId)
                    .Where(x => x.ProjectActivityId == distribution.ProjectActivityId)
                    .Where(x => x.PlanDate >= distribution.FromDate && x.PlanDate <= distribution.ToDate)
                    .OrderBy(x => x.PlanDate)
                    .ThenBy(x => x.ResourcePlanEntryId)
                    .ToListAsync(cancellationToken);

                var groupedEntries = existingEntries
                    .GroupBy(x => x.PlanDate)
                    .ToDictionary(x => x.Key, x => x.OrderBy(y => y.ResourcePlanEntryId).ToList());

                foreach (var entryGroup in groupedEntries)
                {
                    if (!targetHoursByDate.TryGetValue(entryGroup.Key, out var targetHours))
                    {
                        var entryToZero = entryGroup.Value[0];
                        var oldHours = entryToZero.Hours;
                        var oldDescription = entryToZero.Description;
                        var oldIsManualOverride = entryToZero.IsManualOverride;
                        entryToZero.Hours = 0m;
                        entryToZero.UpdatedBy = actor;
                        entryToZero.UpdatedAt = DateTime.UtcNow;
                        touchedIds.Add(entryToZero.ResourcePlanEntryId);
                        historyRecords.Add(BuildHistoryRecord(entryToZero, resourcePlan, oldHours, 0m, oldDescription, entryToZero.Description, oldIsManualOverride, entryToZero.IsManualOverride, "Updated", "ZeroedOutOfRange", caller, correlationId));
                        continue;
                    }

                    var entryToKeep = entryGroup.Value[0];
                    var oldKeepHours = entryToKeep.Hours;
                    var oldKeepDescription = entryToKeep.Description;
                    var oldKeepIsManualOverride = entryToKeep.IsManualOverride;
                    entryToKeep.Hours = targetHours;
                    entryToKeep.Description = NormalizeNullable(distribution.Description);
                    entryToKeep.IsManualOverride = distribution.IsManualOverride;
                    entryToKeep.ProjectActivityId = distribution.ProjectActivityId;
                    entryToKeep.UpdatedBy = actor;
                    entryToKeep.UpdatedAt = DateTime.UtcNow;
                    touchedIds.Add(entryToKeep.ResourcePlanEntryId);
                    historyRecords.Add(BuildHistoryRecord(entryToKeep, resourcePlan, oldKeepHours, targetHours, oldKeepDescription, entryToKeep.Description, oldKeepIsManualOverride, entryToKeep.IsManualOverride, "Updated", "AutoDistribute", caller, correlationId));

                    targetHoursByDate.Remove(entryGroup.Key);
                }

                foreach (var allocation in targetHoursByDate.OrderBy(x => x.Key))
                {
                    var newEntity = new ResourcePlanEntry
                    {
                        ResourcePlanId = distribution.ResourcePlanId,
                        PlanningTargetId = distribution.PlanningTargetId,
                        PlanDate = allocation.Key,
                        Hours = allocation.Value,
                        Description = NormalizeNullable(distribution.Description),
                        IsManualOverride = distribution.IsManualOverride,
                        ProjectActivityId = distribution.ProjectActivityId,
                        CreatedBy = actor,
                        CreatedAt = DateTime.UtcNow
                    };

                    _dbContext.ResourcePlanEntries.Add(newEntity);
                    await _dbContext.SaveChangesAsync(cancellationToken);
                    touchedIds.Add(newEntity.ResourcePlanEntryId);
                    historyRecords.Add(BuildHistoryRecord(newEntity, resourcePlan, null, allocation.Value, null, newEntity.Description, null, newEntity.IsManualOverride, "Created", "AutoDistribute", caller, correlationId));
                }
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            foreach (var record in historyRecords)
            {
                await _historyService.RecordAsync(record, cancellationToken);
            }

            await _changeLogService.RecordChangeAsync(new RecordEntityChangeRequest
            {
                EventType = "ResourcePlanAutoDistributed",
                EventTitle = "Resource plan auto-distributed",
                EntityType = "ResourcePlanDistribution",
                EntityId = correlationId.ToString(),
                PlanningTargetId = request.Distributions.FirstOrDefault()?.PlanningTargetId,
                NewValue = $"{touchedIds.Count} entries affected",
                NewSnapshot = new
                {
                    Distributions = request.Distributions,
                    TouchedResourcePlanEntryIds = touchedIds,
                    HistoryRecordCount = historyRecords.Count
                },
                ChangeReason = "AutoDistribute",
                Caller = caller,
                CorrelationId = correlationId,
                SourceModule = "ResourcePlanEntriesController"
            }, cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            var entries = await _dbContext.ResourcePlanEntries
                .AsNoTracking()
                .Where(x => touchedIds.Contains(x.ResourcePlanEntryId))
                .OrderBy(x => x.PlanDate)
                .ThenBy(x => x.ResourcePlanEntryId)
                .Select(MapToDtoExpression())
                .ToListAsync(cancellationToken);

            return new BulkUpsertResourcePlanEntriesResult
            {
                Entries = entries
            };
        });
    }

    private static RecordResourcePlanEntryHistoryRequest BuildHistoryRecord(
        ResourcePlanEntry entry,
        ResourcePlan? plan,
        decimal? oldHours,
        decimal? newHours,
        string? oldDescription,
        string? newDescription,
        bool? oldIsManualOverride,
        bool? newIsManualOverride,
        string changeType,
        string changeReason,
        CallerInfo caller,
        Guid correlationId)
    {
        return new RecordResourcePlanEntryHistoryRequest
        {
            ResourcePlanEntryId = entry.ResourcePlanEntryId == 0 ? null : entry.ResourcePlanEntryId,
            ResourcePlanId = entry.ResourcePlanId,
            EmployeeId = plan?.EmployeeId ?? 0,
            ScenarioId = plan?.ScenarioId ?? 0,
            PlanningTargetId = entry.PlanningTargetId,
            PlanDate = entry.PlanDate,
            OldHours = oldHours,
            NewHours = newHours,
            OldDescription = oldDescription,
            NewDescription = newDescription,
            OldIsManualOverride = oldIsManualOverride,
            NewIsManualOverride = newIsManualOverride,
            ChangeType = changeType,
            ChangeReason = changeReason,
            ChangedByUserId = caller.UserId,
            ChangedByName = caller.Name,
            SourceModule = "ResourcePlanEntriesController",
            CorrelationId = correlationId
        };
    }

    private static void ValidateHours(decimal hours)
    {
        if (hours < 0)
        {
            throw new InvalidOperationException("Hours cannot be negative.");
        }
    }

    private IQueryable<ResourcePlanEntry> BuildDuplicateQuery(
        int resourcePlanId,
        int planningTargetId,
        DateOnly planDate)
    {
        return _dbContext.ResourcePlanEntries.Where(e =>
            e.ResourcePlanId == resourcePlanId &&
            e.PlanningTargetId == planningTargetId &&
            e.PlanDate == planDate);
    }

    private static void ValidatePlanDate(DateOnly planDate)
    {
        if (planDate == default)
        {
            throw new InvalidOperationException("PlanDate is required.");
        }
    }

    private static string? NormalizeNullable(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string ResolveActor(CallerInfo caller) =>
        !string.IsNullOrWhiteSpace(caller.UserId)
            ? caller.UserId
            : !string.IsNullOrWhiteSpace(caller.Email)
                ? caller.Email
                : caller.Name;

    private static object BuildEntrySnapshot(ResourcePlanEntry entry, ResourcePlan? plan) => new
    {
        entry.ResourcePlanEntryId,
        entry.ResourcePlanId,
        EmployeeId = plan?.EmployeeId,
        ScenarioId = plan?.ScenarioId,
        entry.PlanningTargetId,
        entry.PlanDate,
        entry.Hours,
        entry.Description,
        entry.IsManualOverride,
        entry.CreatedBy,
        entry.UpdatedBy,
        entry.CreatedAt,
        entry.UpdatedAt
    };

    private static Dictionary<DateOnly, decimal> BuildAutoDistribution(
        AutoDistributeResourcePlanEntryItemRequest distribution,
        IReadOnlyDictionary<DateOnly, List<VitaHoliday>> holidayLookup,
        IReadOnlyDictionary<DateOnly, decimal> employeeDailyHours)
    {
        if (distribution.Hours == 0)
        {
            return [];
        }

        var dayCapacities = EnumerateDates(distribution.FromDate, distribution.ToDate)
            .Select(date => new
            {
                Date = date,
                Capacity = GetDayCapacity(date, holidayLookup, employeeDailyHours)
            })
            .Where(x => x.Capacity > 0)
            .ToList();

        if (dayCapacities.Count == 0)
        {
            throw new InvalidOperationException("The selected period does not contain any allocatable working days after applying DK Vita holidays.");
        }

        var totalCapacity = dayCapacities.Sum(x => x.Capacity);
        if (totalCapacity <= 0)
        {
            throw new InvalidOperationException("The selected period does not contain any allocatable working days after applying DK Vita holidays.");
        }

        var totalHundredths = decimal.ToInt64(decimal.Round(distribution.Hours * 100m, 0, MidpointRounding.AwayFromZero));
        var allocations = dayCapacities
            .Select(x => new AutoDistributionAllocation(
                x.Date,
                x.Capacity,
                (totalHundredths * x.Capacity) / totalCapacity))
            .ToList();

        var assigned = allocations.Sum(x => x.BaseHundredths);
        var remainder = totalHundredths - assigned;

        foreach (var allocation in allocations
                     .OrderByDescending(x => x.FractionalHundredths)
                     .ThenBy(x => x.Date)
                     .Take((int)remainder))
        {
            allocation.BaseHundredths++;
        }

        return allocations.ToDictionary(x => x.Date, x => x.BaseHundredths / 100m);
    }

    private static Dictionary<DateOnly, decimal> BuildWorkingDayDistribution(
        SaveResourcePlanEntriesPeriodItemRequest period,
        IReadOnlyDictionary<DateOnly, List<VitaHoliday>> holidayLookup,
        IReadOnlyDictionary<DateOnly, decimal> employeeDailyHours)
    {
        if (period.Hours == 0)
        {
            return [];
        }

        var workingDays = EnumerateDates(period.FromDate, period.ToDate)
            .Where(date => GetDayCapacity(date, holidayLookup, employeeDailyHours) > 0)
            .OrderBy(date => date)
            .ToList();

        if (workingDays.Count == 0)
        {
            throw new InvalidOperationException("The selected period does not contain any allocatable working days after applying DK Vita holidays.");
        }

        var totalHundredths = decimal.ToInt64(decimal.Round(period.Hours * 100m, 0, MidpointRounding.AwayFromZero));
        var baseHundredths = totalHundredths / workingDays.Count;
        var remainder = (int)(totalHundredths % workingDays.Count);
        var result = new Dictionary<DateOnly, decimal>(workingDays.Count);

        for (var index = 0; index < workingDays.Count; index++)
        {
            var allocatedHundredths = baseHundredths + (index < remainder ? 1 : 0);
            result[workingDays[index]] = allocatedHundredths / 100m;
        }

        return result;
    }

    private static IEnumerable<DateOnly> EnumerateDates(DateOnly fromDate, DateOnly toDate)
    {
        for (var current = fromDate; current <= toDate; current = current.AddDays(1))
        {
            yield return current;
        }
    }

    /// <summary>
    /// The employee's actual capacity for the day, in hours: their resolved profile/override
    /// hours for that weekday, reduced by any DK Vita holiday affecting that date. Weekends are
    /// not hard-coded to zero here — an employee whose profile explicitly sets Saturday/Sunday
    /// hours is respected; everyone else's employeeDailyHours is already 0 on those days.
    /// </summary>
    private static decimal GetDayCapacity(
        DateOnly date,
        IReadOnlyDictionary<DateOnly, List<VitaHoliday>> holidayLookup,
        IReadOnlyDictionary<DateOnly, decimal> employeeDailyHours)
    {
        var hours = employeeDailyHours.TryGetValue(date, out var h) ? h : 0m;

        if (hours <= 0m)
        {
            return 0m;
        }

        return GetHolidayFraction(date, holidayLookup) * hours;
    }

    private static decimal GetHolidayFraction(
        DateOnly date,
        IReadOnlyDictionary<DateOnly, List<VitaHoliday>> holidayLookup)
    {
        var capacity = 1m;

        if (!holidayLookup.TryGetValue(date, out var holidays))
        {
            return capacity;
        }

        foreach (var holiday in holidays)
        {
            if (holiday.IsActive == false)
            {
                continue;
            }

            if (holiday.IsHalfDay == true)
            {
                capacity = Math.Min(capacity, 0.5m);
            }

            if (holiday.HoursReduction.HasValue)
            {
                var normalizedCapacity = Math.Clamp((8m - holiday.HoursReduction.Value) / 8m, 0m, 1m);
                capacity = Math.Min(capacity, normalizedCapacity);
            }

            if (holiday.IsHalfDay != true && !holiday.HoursReduction.HasValue)
            {
                capacity = 0m;
            }
        }

        return capacity;
    }

    private sealed class AutoDistributionAllocation
    {
        public AutoDistributionAllocation(DateOnly date, decimal capacity, decimal exactHundredths)
        {
            Date = date;
            Capacity = capacity;
            BaseHundredths = decimal.ToInt64(decimal.Floor(exactHundredths));
            FractionalHundredths = exactHundredths - BaseHundredths;
        }

        public DateOnly Date { get; }
        public decimal Capacity { get; }
        public long BaseHundredths { get; set; }
        public decimal FractionalHundredths { get; }
    }

    private async Task<ResourcePlanEntryDto> MapToDtoWithActivityAsync(
        ResourcePlanEntry entity,
        ResourcePlan resourcePlan,
        CancellationToken cancellationToken)
    {
        int? activityNumber = null;
        string? activityName = null;

        if (entity.ProjectActivityId.HasValue)
        {
            var activityInfo = await (
                from projectActivity in _dbContext.ProjectActivities.AsNoTracking()
                join activity in _dbContext.Activities.AsNoTracking()
                    on projectActivity.ActivityNumber equals activity.ActivityNumber
                where projectActivity.Number == entity.ProjectActivityId.Value
                select new { activity.ActivityNumber, activity.Name }
            ).FirstOrDefaultAsync(cancellationToken);

            activityNumber = activityInfo?.ActivityNumber;
            activityName = activityInfo?.Name;
        }

        return new ResourcePlanEntryDto
        {
            ResourcePlanEntryId = entity.ResourcePlanEntryId,
            ResourcePlanId = entity.ResourcePlanId,
            EmployeeId = resourcePlan.EmployeeId,
            VirtualResourceId = resourcePlan.VirtualResourceId,
            ScenarioId = resourcePlan.ScenarioId,
            Hours = entity.Hours,
            Description = entity.Description,
            IsManualOverride = entity.IsManualOverride,
            CreatedBy = entity.CreatedBy,
            UpdatedBy = entity.UpdatedBy,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt,
            PlanningTargetId = entity.PlanningTargetId,
            PlanningTargetCode = entity.PlanningTarget?.Code,
            PlanningTargetName = entity.PlanningTarget?.Name,
            PlanningTargetType = entity.PlanningTarget?.TargetType,
            ProjectNumber = entity.PlanningTarget?.ExtProjectNumber,
            OfferId = entity.PlanningTarget?.OfferId,
            InternalPlanningCodeId = entity.PlanningTarget?.InternalPlanningCodeId,
            PlanDate = entity.PlanDate,
            ProjectActivityId = entity.ProjectActivityId,
            ActivityNumber = activityNumber,
            ActivityName = activityName
        };
    }

    private Expression<Func<ResourcePlanEntry, ResourcePlanEntryDto>> MapToDtoExpression()
    {
        return entity => new ResourcePlanEntryDto
        {
            ResourcePlanEntryId = entity.ResourcePlanEntryId,
            ResourcePlanId = entity.ResourcePlanId,
            EmployeeId = entity.ResourcePlan != null ? entity.ResourcePlan.EmployeeId : null,
            VirtualResourceId = entity.ResourcePlan != null ? entity.ResourcePlan.VirtualResourceId : null,
            ScenarioId = entity.ResourcePlan != null ? entity.ResourcePlan.ScenarioId : 0,
            Hours = entity.Hours,
            Description = entity.Description,
            IsManualOverride = entity.IsManualOverride,
            CreatedBy = entity.CreatedBy,
            UpdatedBy = entity.UpdatedBy,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt,
            PlanningTargetId = entity.PlanningTargetId,
            PlanningTargetCode = entity.PlanningTarget != null ? entity.PlanningTarget.Code : null,
            PlanningTargetName = entity.PlanningTarget != null ? entity.PlanningTarget.Name : null,
            PlanningTargetType = entity.PlanningTarget != null ? entity.PlanningTarget.TargetType : null,
            ProjectNumber = entity.PlanningTarget != null ? entity.PlanningTarget.ExtProjectNumber : null,
            OfferId = entity.PlanningTarget != null ? entity.PlanningTarget.OfferId : null,
            InternalPlanningCodeId = entity.PlanningTarget != null ? entity.PlanningTarget.InternalPlanningCodeId : null,
            PlanDate = entity.PlanDate,
            ProjectActivityId = entity.ProjectActivityId,
            ActivityNumber = entity.ProjectActivity != null ? (int?)entity.ProjectActivity.ActivityNumber : null,
            ActivityName = entity.ProjectActivity != null && entity.ProjectActivity.Activity != null
                ? entity.ProjectActivity.Activity.Name
                : null
        };
    }
}
