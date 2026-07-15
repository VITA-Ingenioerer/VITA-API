using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Vita.Planning.Application.DTOs;
using Vita.Planning.Application.Interfaces;
using Vita.Planning.Infrastructure.Data;
using Vita.Planning.Infrastructure.Data.Entities;

namespace Vita.Planning.Infrastructure.Services;

public sealed class ResourcePlanSnapshotService : IResourcePlanSnapshotService
{
    private readonly PlanningDbContext _dbContext;
    private readonly IEntityChangeLogService _changeLog;

    public ResourcePlanSnapshotService(
        PlanningDbContext dbContext,
        IEntityChangeLogService changeLog)
    {
        _dbContext = dbContext;
        _changeLog = changeLog;
    }

    public async Task<ResourcePlanSnapshotDto> CreateAsync(
        CreateResourcePlanSnapshotRequest request,
        CallerInfo caller,
        CancellationToken cancellationToken = default)
    {
        var periodType = NormalizePeriodType(request.PeriodType);
        var snapshotType = NormalizeRequired(request.SnapshotType, "Manual", 30);
        var snapshotAsOfUtc = DateTime.UtcNow;

        var query =
            from entry in _dbContext.ResourcePlanEntries.AsNoTracking()
            join plan in _dbContext.ResourcePlans.AsNoTracking()
                on entry.ResourcePlanId equals plan.ResourcePlanId
            join target in _dbContext.PlanningTargets.AsNoTracking()
                on entry.PlanningTargetId equals target.PlanningTargetId into targetJoin
            from target in targetJoin.DefaultIfEmpty()
            // Snapshots don't support virtual-resource-backed plans yet (resource_plan_snapshot_entries.employee_id
            // is still required); exclude them rather than persisting a bogus employee id.
            where plan.ScenarioId == request.ScenarioId && plan.EmployeeId != null
            select new SnapshotSourceRow
            {
                ResourcePlanId = entry.ResourcePlanId,
                ResourcePlanEntryId = entry.ResourcePlanEntryId,
                EmployeeId = plan.EmployeeId!.Value,
                ScenarioId = plan.ScenarioId,
                PlanningTargetId = entry.PlanningTargetId,
                ProjectNumber = target != null ? target.ExtProjectNumber : null,
                PlanningCode = target != null ? target.Code : null,
                DisplayText = target != null ? target.Name : null,
                PlanDate = entry.PlanDate,
                Hours = entry.Hours,
                Description = entry.Description,
                IsManualOverride = entry.IsManualOverride
            };

        if (request.FromDate.HasValue)
        {
            query = query.Where(x => x.PlanDate >= request.FromDate.Value);
        }

        if (request.ToDate.HasValue)
        {
            query = query.Where(x => x.PlanDate <= request.ToDate.Value);
        }

        var sourceRows = await query.ToListAsync(cancellationToken);

        var snapshot = new ResourcePlanSnapshot
        {
            ScenarioId = request.ScenarioId,
            SnapshotName = NormalizeNullable(request.SnapshotName, 200),
            SnapshotType = snapshotType,
            SnapshotAsOfUtc = snapshotAsOfUtc,
            CreatedBy = ResolveActor(caller),
            Notes = NormalizeNullable(request.Notes, 1000)
        };

        _dbContext.ResourcePlanSnapshots.Add(snapshot);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var entries = sourceRows
            .GroupBy(row => BuildSnapshotKey(row, periodType))
            .Select(group => new ResourcePlanSnapshotEntry
            {
                ResourcePlanSnapshotId = snapshot.ResourcePlanSnapshotId,
                ResourcePlanId = group.Key.ResourcePlanId,
                ResourcePlanEntryId = null,
                EmployeeId = group.Key.EmployeeId,
                ScenarioId = group.Key.ScenarioId,
                PlanningTargetId = group.Key.PlanningTargetId,
                ProjectNumber = group.Key.ProjectNumber,
                PlanningCode = NormalizeNullable(group.Key.PlanningCode, 30),
                DisplayText = NormalizeNullable(group.Key.DisplayText, 150),
                YearNumber = group.Key.YearNumber,
                MonthNumber = group.Key.MonthNumber,
                WeekNumber = group.Key.WeekNumber,
                PeriodType = periodType,
                Hours = group.Sum(x => x.Hours),
                Description = NormalizeNullable(group.Select(x => x.Description).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)), 255),
                IsManualOverride = group.Any(x => x.IsManualOverride),
                SnapshotAsOfUtc = snapshotAsOfUtc
            })
            .Where(x => x.Hours != 0)
            .OrderBy(x => x.EmployeeId)
            .ThenBy(x => x.YearNumber)
            .ThenBy(x => x.MonthNumber)
            .ThenBy(x => x.WeekNumber)
            .ThenBy(x => x.PlanningCode)
            .ToList();

        _dbContext.ResourcePlanSnapshotEntries.AddRange(entries);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var dto = await GetSnapshotDtoAsync(snapshot.ResourcePlanSnapshotId, cancellationToken)
                  ?? MapToDto(snapshot, entries.Count, entries.Sum(x => x.Hours));

        await _changeLog.RecordChangeAsync(new RecordEntityChangeRequest
        {
            EventType = "ResourcePlanSnapshotCreated",
            EventTitle = $"Ressourceplan snapshot oprettet: {snapshot.SnapshotName ?? snapshot.ResourcePlanSnapshotId.ToString(CultureInfo.InvariantCulture)}",
            EntityType = "ResourcePlanSnapshot",
            EntityId = snapshot.ResourcePlanSnapshotId.ToString(CultureInfo.InvariantCulture),
            NewValue = $"{dto.EntryCount} entries / {dto.TotalHours:0.##} timer",
            NewSnapshot = dto,
            Caller = caller,
            SourceModule = "ResourcePlanSnapshotService"
        }, cancellationToken);

        return dto;
    }

    public async Task<IReadOnlyList<ResourcePlanSnapshotDto>> GetAllAsync(
        int? scenarioId = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.ResourcePlanSnapshots.AsNoTracking();

        if (scenarioId.HasValue)
        {
            query = query.Where(x => x.ScenarioId == scenarioId.Value);
        }

        return await query
            .GroupJoin(
                _dbContext.ResourcePlanSnapshotEntries.AsNoTracking(),
                snapshot => snapshot.ResourcePlanSnapshotId,
                entry => entry.ResourcePlanSnapshotId,
                (snapshot, entries) => new ResourcePlanSnapshotDto
                {
                    ResourcePlanSnapshotId = snapshot.ResourcePlanSnapshotId,
                    ScenarioId = snapshot.ScenarioId,
                    SnapshotName = snapshot.SnapshotName,
                    SnapshotType = snapshot.SnapshotType,
                    SnapshotAsOfUtc = snapshot.SnapshotAsOfUtc,
                    CreatedBy = snapshot.CreatedBy,
                    Notes = snapshot.Notes,
                    EntryCount = entries.Count(),
                    TotalHours = entries.Sum(x => (decimal?)x.Hours) ?? 0m
                })
            .OrderByDescending(x => x.SnapshotAsOfUtc)
            .ThenByDescending(x => x.ResourcePlanSnapshotId)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ResourcePlanSnapshotEntryDto>> GetEntriesAsync(
        long snapshotId,
        int? employeeId = null,
        int? planningTargetId = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.ResourcePlanSnapshotEntries
            .AsNoTracking()
            .Where(x => x.ResourcePlanSnapshotId == snapshotId);

        if (employeeId.HasValue)
        {
            query = query.Where(x => x.EmployeeId == employeeId.Value);
        }

        if (planningTargetId.HasValue)
        {
            query = query.Where(x => x.PlanningTargetId == planningTargetId.Value);
        }

        return await query
            .OrderBy(x => x.EmployeeId)
            .ThenBy(x => x.YearNumber)
            .ThenBy(x => x.MonthNumber)
            .ThenBy(x => x.WeekNumber)
            .ThenBy(x => x.PlanningCode)
            .Select(x => MapEntryToDto(x))
            .ToListAsync(cancellationToken);
    }

    public async Task<ResourcePlanSnapshotComparisonDto> CompareAsync(
        long fromSnapshotId,
        long toSnapshotId,
        int? employeeId = null,
        int? planningTargetId = null,
        CancellationToken cancellationToken = default)
    {
        var fromEntries = await GetEntriesAsync(fromSnapshotId, employeeId, planningTargetId, cancellationToken);
        var toEntries = await GetEntriesAsync(toSnapshotId, employeeId, planningTargetId, cancellationToken);

        var fromLookup = fromEntries
            .GroupBy(BuildComparisonKey)
            .ToDictionary(x => x.Key, x => x.Sum(y => y.Hours));

        var toLookup = toEntries
            .GroupBy(BuildComparisonKey)
            .ToDictionary(x => x.Key, x => new
            {
                Hours = x.Sum(y => y.Hours),
                Sample = x.First()
            });

        var keys = fromLookup.Keys.Union(toLookup.Keys).ToList();
        var changes = new List<ResourcePlanSnapshotChangeDto>();

        foreach (var key in keys)
        {
            fromLookup.TryGetValue(key, out var oldHours);
            toLookup.TryGetValue(key, out var toValue);
            var newHours = toValue?.Hours ?? 0m;
            var delta = newHours - oldHours;

            if (delta == 0)
            {
                continue;
            }

            var sample = toValue?.Sample ?? fromEntries.First(x => BuildComparisonKey(x) == key);
            changes.Add(new ResourcePlanSnapshotChangeDto
            {
                ResourcePlanId = sample.ResourcePlanId,
                EmployeeId = sample.EmployeeId,
                PlanningTargetId = sample.PlanningTargetId,
                ProjectNumber = sample.ProjectNumber,
                PlanningCode = sample.PlanningCode,
                DisplayText = sample.DisplayText,
                YearNumber = sample.YearNumber,
                MonthNumber = sample.MonthNumber,
                WeekNumber = sample.WeekNumber,
                PeriodType = sample.PeriodType,
                OldHours = oldHours,
                NewHours = newHours,
                DeltaHours = delta,
                ChangeType = oldHours == 0 ? "Added" : newHours == 0 ? "Removed" : "Changed"
            });
        }

        return new ResourcePlanSnapshotComparisonDto
        {
            FromSnapshotId = fromSnapshotId,
            ToSnapshotId = toSnapshotId,
            Changes = changes
                .OrderByDescending(x => Math.Abs(x.DeltaHours))
                .ThenBy(x => x.EmployeeId)
                .ThenBy(x => x.PlanningCode)
                .ToList()
        };
    }

    private async Task<ResourcePlanSnapshotDto?> GetSnapshotDtoAsync(
        long snapshotId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.ResourcePlanSnapshots
            .AsNoTracking()
            .Where(x => x.ResourcePlanSnapshotId == snapshotId)
            .GroupJoin(
                _dbContext.ResourcePlanSnapshotEntries.AsNoTracking(),
                snapshot => snapshot.ResourcePlanSnapshotId,
                entry => entry.ResourcePlanSnapshotId,
                (snapshot, entries) => new ResourcePlanSnapshotDto
                {
                    ResourcePlanSnapshotId = snapshot.ResourcePlanSnapshotId,
                    ScenarioId = snapshot.ScenarioId,
                    SnapshotName = snapshot.SnapshotName,
                    SnapshotType = snapshot.SnapshotType,
                    SnapshotAsOfUtc = snapshot.SnapshotAsOfUtc,
                    CreatedBy = snapshot.CreatedBy,
                    Notes = snapshot.Notes,
                    EntryCount = entries.Count(),
                    TotalHours = entries.Sum(x => (decimal?)x.Hours) ?? 0m
                })
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static SnapshotKey BuildSnapshotKey(SnapshotSourceRow row, string periodType)
    {
        var date = row.PlanDate.ToDateTime(TimeOnly.MinValue);
        var weekNumber = ISOWeek.GetWeekOfYear(date);
        var weekYear = ISOWeek.GetYear(date);

        return periodType switch
        {
            "Week" => new SnapshotKey(
                row.ResourcePlanId,
                row.EmployeeId,
                row.ScenarioId,
                row.PlanningTargetId,
                row.ProjectNumber,
                row.PlanningCode,
                row.DisplayText,
                weekYear,
                null,
                weekNumber),
            _ => new SnapshotKey(
                row.ResourcePlanId,
                row.EmployeeId,
                row.ScenarioId,
                row.PlanningTargetId,
                row.ProjectNumber,
                row.PlanningCode,
                row.DisplayText,
                row.PlanDate.Year,
                row.PlanDate.Month,
                null)
        };
    }

    private static string BuildComparisonKey(ResourcePlanSnapshotEntryDto entry) =>
        string.Join('|',
            entry.ResourcePlanId,
            entry.EmployeeId,
            entry.PlanningTargetId,
            entry.ProjectNumber,
            entry.PlanningCode,
            entry.YearNumber,
            entry.MonthNumber,
            entry.WeekNumber,
            entry.PeriodType);

    private static string NormalizePeriodType(string? value)
    {
        var normalized = (value ?? "Month").Trim();
        return normalized.Equals("Week", StringComparison.OrdinalIgnoreCase)
            ? "Week"
            : "Month";
    }

    private static string NormalizeRequired(string? value, string fallback, int maxLength) =>
        NormalizeNullable(value, maxLength) ?? fallback;

    private static string? NormalizeNullable(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
    }

    private static string ResolveActor(CallerInfo caller) =>
        !string.IsNullOrWhiteSpace(caller.UserId)
            ? caller.UserId
            : !string.IsNullOrWhiteSpace(caller.Email)
                ? caller.Email
                : caller.Name;

    private static ResourcePlanSnapshotDto MapToDto(ResourcePlanSnapshot entity, int entryCount, decimal totalHours) => new()
    {
        ResourcePlanSnapshotId = entity.ResourcePlanSnapshotId,
        ScenarioId = entity.ScenarioId,
        SnapshotName = entity.SnapshotName,
        SnapshotType = entity.SnapshotType,
        SnapshotAsOfUtc = entity.SnapshotAsOfUtc,
        CreatedBy = entity.CreatedBy,
        Notes = entity.Notes,
        EntryCount = entryCount,
        TotalHours = totalHours
    };

    private static ResourcePlanSnapshotEntryDto MapEntryToDto(ResourcePlanSnapshotEntry entity) => new()
    {
        ResourcePlanSnapshotEntryId = entity.ResourcePlanSnapshotEntryId,
        ResourcePlanSnapshotId = entity.ResourcePlanSnapshotId,
        ResourcePlanId = entity.ResourcePlanId,
        ResourcePlanEntryId = entity.ResourcePlanEntryId,
        EmployeeId = entity.EmployeeId,
        ScenarioId = entity.ScenarioId,
        PlanningTargetId = entity.PlanningTargetId,
        ProjectNumber = entity.ProjectNumber,
        PlanningCode = entity.PlanningCode,
        DisplayText = entity.DisplayText,
        YearNumber = entity.YearNumber,
        MonthNumber = entity.MonthNumber,
        WeekNumber = entity.WeekNumber,
        PeriodType = entity.PeriodType,
        Hours = entity.Hours,
        Description = entity.Description,
        IsManualOverride = entity.IsManualOverride,
        SnapshotAsOfUtc = entity.SnapshotAsOfUtc
    };

    private sealed class SnapshotSourceRow
    {
        public int ResourcePlanId { get; init; }
        public int ResourcePlanEntryId { get; init; }
        public int EmployeeId { get; init; }
        public int ScenarioId { get; init; }
        public int? PlanningTargetId { get; init; }
        public int? ProjectNumber { get; init; }
        public string? PlanningCode { get; init; }
        public string? DisplayText { get; init; }
        public DateOnly PlanDate { get; init; }
        public decimal Hours { get; init; }
        public string? Description { get; init; }
        public bool IsManualOverride { get; init; }
    }

    private sealed record SnapshotKey(
        int ResourcePlanId,
        int EmployeeId,
        int ScenarioId,
        int? PlanningTargetId,
        int? ProjectNumber,
        string? PlanningCode,
        string? DisplayText,
        int YearNumber,
        int? MonthNumber,
        int? WeekNumber);
}
