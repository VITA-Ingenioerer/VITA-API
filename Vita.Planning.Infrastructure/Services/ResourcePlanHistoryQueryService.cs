using Microsoft.EntityFrameworkCore;
using Vita.Planning.Application.DTOs;
using Vita.Planning.Application.Interfaces;
using Vita.Planning.Infrastructure.Data;
using Vita.Planning.Infrastructure.Data.Entities;

namespace Vita.Planning.Infrastructure.Services;

public sealed class ResourcePlanHistoryQueryService : IResourcePlanHistoryQueryService
{
    private readonly PlanningDbContext _dbContext;

    public ResourcePlanHistoryQueryService(PlanningDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PagedResultDto<ResourcePlanLogbookEntryDto>> GetLogbookAsync(
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        int? employeeId = null,
        int? planningTargetId = null,
        string? changedByUserId = null,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize < 1 ? 50 : Math.Min(pageSize, 200);

        var query = _dbContext.ResourcePlanEntryHistories.AsNoTracking().AsQueryable();

        if (fromUtc.HasValue)
            query = query.Where(x => x.ChangedAtUtc >= fromUtc.Value);

        if (toUtc.HasValue)
            query = query.Where(x => x.ChangedAtUtc <= toUtc.Value);

        if (employeeId.HasValue)
            query = query.Where(x => x.EmployeeId == employeeId.Value);

        if (planningTargetId.HasValue)
            query = query.Where(x => x.PlanningTargetId == planningTargetId.Value);

        if (!string.IsNullOrWhiteSpace(changedByUserId))
            query = query.Where(x => x.ChangedByUserId == changedByUserId);

        var grouped = query
            .GroupBy(x => x.CorrelationId)
            .Select(g => new { CorrelationId = g.Key, MaxChangedAtUtc = g.Max(x => x.ChangedAtUtc) });

        var totalCount = await grouped.CountAsync(cancellationToken);

        var pageCorrelationIds = await grouped
            .OrderByDescending(x => x.MaxChangedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => x.CorrelationId)
            .ToListAsync(cancellationToken);

        var rows = await _dbContext.ResourcePlanEntryHistories
            .AsNoTracking()
            .Where(x => pageCorrelationIds.Contains(x.CorrelationId))
            .ToListAsync(cancellationToken);

        var items = rows
            .GroupBy(x => x.CorrelationId)
            .Select(g =>
            {
                var first = g.OrderBy(x => x.ChangedAtUtc).First();
                var distinctChangeTypes = g.Select(x => x.ChangeType).Distinct().ToList();

                return new ResourcePlanLogbookEntryDto
                {
                    CorrelationId = g.Key,
                    ChangedAtUtc = g.Max(x => x.ChangedAtUtc),
                    ChangedByUserId = first.ChangedByUserId,
                    ChangedByName = first.ChangedByName,
                    SourceModule = first.SourceModule,
                    ChangeReason = first.ChangeReason,
                    ChangeType = distinctChangeTypes.Count == 1 ? distinctChangeTypes[0] : "Mixed",
                    EntryCount = g.Count(),
                    TotalHoursDelta = g.Sum(x => (x.NewHours ?? 0m) - (x.OldHours ?? 0m)),
                    EmployeeIds = g.Select(x => x.EmployeeId).Distinct().OrderBy(x => x).ToList(),
                    PlanningTargetIds = g.Select(x => x.PlanningTargetId).Distinct().OrderBy(x => x).ToList(),
                    PlanDateFrom = g.Min(x => x.PlanDate),
                    PlanDateTo = g.Max(x => x.PlanDate)
                };
            })
            .OrderByDescending(x => x.ChangedAtUtc)
            .ToList();

        return new PagedResultDto<ResourcePlanLogbookEntryDto>
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize),
            Items = items
        };
    }

    public async Task<IReadOnlyList<ResourcePlanAsOfEntryDto>> GetStateAsOfAsync(
        DateTime asOfUtc,
        DateOnly periodFrom,
        DateOnly periodTo,
        int? employeeId = null,
        int? scenarioId = null,
        CancellationToken cancellationToken = default)
    {
        var latest = await LoadLatestPerEntryAsOfAsync(asOfUtc, periodFrom, periodTo, employeeId, scenarioId, cancellationToken);

        var planningTargetIds = latest.Select(x => x.PlanningTargetId).Distinct().ToList();
        var targets = await _dbContext.PlanningTargets
            .AsNoTracking()
            .Where(x => planningTargetIds.Contains(x.PlanningTargetId))
            .ToDictionaryAsync(x => x.PlanningTargetId, cancellationToken);

        return latest
            .Select(x =>
            {
                targets.TryGetValue(x.PlanningTargetId, out var target);
                return new ResourcePlanAsOfEntryDto
                {
                    EmployeeId = x.EmployeeId,
                    ScenarioId = x.ScenarioId,
                    PlanningTargetId = x.PlanningTargetId,
                    PlanningTargetCode = target?.Code,
                    PlanningTargetName = target?.Name,
                    ProjectNumber = target?.ExtProjectNumber,
                    PlanDate = x.PlanDate,
                    Hours = x.NewHours ?? 0m,
                    Description = x.NewDescription,
                    IsManualOverride = x.NewIsManualOverride ?? false
                };
            })
            .OrderBy(x => x.EmployeeId)
            .ThenBy(x => x.PlanDate)
            .ThenBy(x => x.PlanningTargetId)
            .ToList();
    }

    public async Task<IReadOnlyList<ResourcePlanAsOfComparisonEntryDto>> CompareAsOfAsync(
        DateTime asOfFromUtc,
        DateTime asOfToUtc,
        DateOnly periodFrom,
        DateOnly periodTo,
        int? employeeId = null,
        int? scenarioId = null,
        CancellationToken cancellationToken = default)
    {
        var fromState = await GetStateAsOfAsync(asOfFromUtc, periodFrom, periodTo, employeeId, scenarioId, cancellationToken);
        var toState = await GetStateAsOfAsync(asOfToUtc, periodFrom, periodTo, employeeId, scenarioId, cancellationToken);

        var fromLookup = fromState.ToDictionary(x => (x.EmployeeId, x.PlanningTargetId, x.PlanDate), x => x);
        var toLookup = toState.ToDictionary(x => (x.EmployeeId, x.PlanningTargetId, x.PlanDate), x => x);

        var changes = new List<ResourcePlanAsOfComparisonEntryDto>();

        foreach (var key in fromLookup.Keys.Union(toLookup.Keys))
        {
            fromLookup.TryGetValue(key, out var fromEntry);
            toLookup.TryGetValue(key, out var toEntry);

            var oldHours = fromEntry?.Hours ?? 0m;
            var newHours = toEntry?.Hours ?? 0m;
            var delta = newHours - oldHours;

            if (delta == 0m)
                continue;

            var sample = toEntry ?? fromEntry!;
            changes.Add(new ResourcePlanAsOfComparisonEntryDto
            {
                EmployeeId = key.Item1,
                PlanningTargetId = key.Item2,
                PlanningTargetCode = sample.PlanningTargetCode,
                PlanningTargetName = sample.PlanningTargetName,
                ProjectNumber = sample.ProjectNumber,
                PlanDate = key.Item3,
                OldHours = oldHours,
                NewHours = newHours,
                DeltaHours = delta
            });
        }

        return changes
            .OrderByDescending(x => Math.Abs(x.DeltaHours))
            .ThenBy(x => x.EmployeeId)
            .ThenBy(x => x.PlanDate)
            .ToList();
    }

    /// <summary>
    /// For every resource plan entry touched at or before <paramref name="asOfUtc"/> within the
    /// given scope, returns its latest history row as of that moment — i.e. the entry's true state
    /// at that point in time. Entries whose latest known value is zero hours are dropped (matches
    /// "the entry effectively didn't exist" — the app never hard-deletes entries, it zeroes them).
    /// Grouping-by-latest is done in memory: history volume for a bounded employee/date scope is
    /// small enough that this is simpler and more reliable than coaxing EF Core into translating a
    /// "first row per group, ordered" query.
    /// </summary>
    private async Task<List<ResourcePlanEntryHistory>> LoadLatestPerEntryAsOfAsync(
        DateTime asOfUtc,
        DateOnly periodFrom,
        DateOnly periodTo,
        int? employeeId,
        int? scenarioId,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.ResourcePlanEntryHistories
            .AsNoTracking()
            .Where(x => x.ChangedAtUtc <= asOfUtc)
            .Where(x => x.PlanDate >= periodFrom && x.PlanDate <= periodTo);

        if (employeeId.HasValue)
            query = query.Where(x => x.EmployeeId == employeeId.Value);

        if (scenarioId.HasValue)
            query = query.Where(x => x.ScenarioId == scenarioId.Value);

        var rows = await query
            .Where(x => x.ResourcePlanEntryId != null)
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(x => x.ResourcePlanEntryId!.Value)
            .Select(g => g
                .OrderByDescending(x => x.ChangedAtUtc)
                .ThenByDescending(x => x.ResourcePlanEntryHistoryId)
                .First())
            .Where(x => (x.NewHours ?? 0m) != 0m)
            .ToList();
    }
}
