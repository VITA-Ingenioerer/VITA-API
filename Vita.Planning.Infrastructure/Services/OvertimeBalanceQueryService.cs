using Microsoft.EntityFrameworkCore;
using Vita.Planning.Application.DTOs;
using Vita.Planning.Application.Interfaces;
using Vita.Planning.Infrastructure.Data;

namespace Vita.Planning.Infrastructure.Services;

public sealed class OvertimeBalanceQueryService : IOvertimeBalanceQueryService
{
    // "Current balance" only needs the most recent tracked row per employee — RunningBalance
    // on that row already IS the full cumulative total, so older rows add nothing but load.
    // The materialized table is small/indexed so this isn't load-bearing anymore (it was,
    // against the old live view), but it's a harmless bound to keep: any employee with an
    // active profile gets a row for today on every refresh, so this never excludes anyone
    // whose data is actually up to date.
    private static readonly int CurrentBalanceLookbackDays = 90;

    private readonly PlanningDbContext _dbContext;

    public OvertimeBalanceQueryService(PlanningDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<OvertimeBalanceDayDto>> GetDailyBalanceAsync(
        int employeeId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        var displayName = await GetDisplayNameAsync(employeeId, cancellationToken);

        return await _dbContext.OvertimeBalanceDaily
            .AsNoTracking()
            .Where(x => x.EmployeeId == employeeId && x.WorkDate >= from && x.WorkDate <= to)
            .OrderBy(x => x.WorkDate)
            .Select(x => new OvertimeBalanceDayDto
            {
                EmployeeId = x.EmployeeId,
                DisplayName = displayName,
                WorkDate = x.WorkDate,
                ActualHours = x.ActualHours,
                ExpectedHours = x.ExpectedHours,
                AdjustmentHours = x.AdjustmentHours,
                DailyDelta = x.DailyDelta,
                RunningBalance = x.RunningBalance
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<OvertimeBalanceSummaryDto?> GetCurrentBalanceAsync(
        int employeeId, CancellationToken cancellationToken = default)
    {
        var lookbackFrom = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-CurrentBalanceLookbackDays));

        var latest = await _dbContext.OvertimeBalanceDaily
            .AsNoTracking()
            .Where(x => x.EmployeeId == employeeId && x.WorkDate >= lookbackFrom)
            .OrderByDescending(x => x.WorkDate)
            .FirstOrDefaultAsync(cancellationToken);

        if (latest is null)
        {
            return null;
        }

        return new OvertimeBalanceSummaryDto
        {
            EmployeeId = latest.EmployeeId,
            DisplayName = await GetDisplayNameAsync(employeeId, cancellationToken),
            AsOfDate = latest.WorkDate,
            RunningBalance = latest.RunningBalance
        };
    }

    public async Task<IReadOnlyList<OvertimeBalanceSummaryDto>> GetCurrentBalancesAsync(
        IReadOnlyCollection<int>? employeeIds, CancellationToken cancellationToken = default)
    {
        var lookbackFrom = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-CurrentBalanceLookbackDays));
        var query = _dbContext.OvertimeBalanceDaily.AsNoTracking().Where(x => x.WorkDate >= lookbackFrom);
        if (employeeIds is { Count: > 0 })
        {
            query = query.Where(x => employeeIds.Contains(x.EmployeeId));
        }

        var latestDatePerEmployee = query
            .GroupBy(x => x.EmployeeId)
            .Select(g => new { EmployeeId = g.Key, WorkDate = g.Max(x => x.WorkDate) });

        var latestRows =
            from row in query
            join latest in latestDatePerEmployee
                on new { row.EmployeeId, row.WorkDate } equals new { latest.EmployeeId, latest.WorkDate }
            select row;

        var results = await latestRows
            .Select(x => new OvertimeBalanceSummaryDto
            {
                EmployeeId = x.EmployeeId,
                AsOfDate = x.WorkDate,
                RunningBalance = x.RunningBalance
            })
            .ToListAsync(cancellationToken);

        if (results.Count == 0)
        {
            return results;
        }

        var resultEmployeeIds = results.Select(r => r.EmployeeId).ToList();
        var displayNames = await _dbContext.Users
            .AsNoTracking()
            .Where(u => resultEmployeeIds.Contains(u.EmployeeId))
            .ToDictionaryAsync(u => u.EmployeeId, u => u.DisplayName, cancellationToken);

        foreach (var result in results)
        {
            result.DisplayName = displayNames.GetValueOrDefault(result.EmployeeId);
        }

        return results;
    }

    public async Task<IReadOnlyList<OvertimeTrendPointDto>> GetTrendAsync(
        DateOnly from, DateOnly to, IReadOnlyCollection<int>? employeeIds, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.OvertimeBalanceDaily
            .AsNoTracking()
            .Where(x => x.WorkDate >= from && x.WorkDate <= to);

        if (employeeIds is { Count: > 0 })
        {
            query = query.Where(x => employeeIds.Contains(x.EmployeeId));
        }

        return await query
            .GroupBy(x => x.WorkDate)
            .Select(g => new OvertimeTrendPointDto
            {
                WorkDate = g.Key,
                TotalBalance = g.Sum(x => x.RunningBalance),
                AverageBalance = g.Average(x => x.RunningBalance),
                EmployeeCount = g.Count()
            })
            .OrderBy(x => x.WorkDate)
            .ToListAsync(cancellationToken);
    }

    private async Task<string?> GetDisplayNameAsync(int employeeId, CancellationToken cancellationToken) =>
        await _dbContext.Users
            .AsNoTracking()
            .Where(u => u.EmployeeId == employeeId)
            .Select(u => u.DisplayName)
            .FirstOrDefaultAsync(cancellationToken);
}
