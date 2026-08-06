using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using Vita.Planning.Application.DTOs;
using Vita.Planning.Application.Interfaces;
using Vita.Planning.Infrastructure.Data;
using Vita.Planning.Infrastructure.Data.Entities;

namespace Vita.Planning.Infrastructure.Services;

public sealed class OvertimeBalanceQueryService : IOvertimeBalanceQueryService
{
    private readonly PlanningDbContext _dbContext;

    public OvertimeBalanceQueryService(PlanningDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<OvertimeBalanceDayDto>> GetDailyBalanceAsync(
        int employeeId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        return await _dbContext.OvertimeBalanceDaily
            .AsNoTracking()
            .Where(x => x.EmployeeId == employeeId && x.WorkDate >= from && x.WorkDate <= to)
            .OrderBy(x => x.WorkDate)
            .Select(MapToDtoExpression())
            .ToListAsync(cancellationToken);
    }

    public async Task<OvertimeBalanceSummaryDto?> GetCurrentBalanceAsync(
        int employeeId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.OvertimeBalanceDaily
            .AsNoTracking()
            .Where(x => x.EmployeeId == employeeId)
            .OrderByDescending(x => x.WorkDate)
            .Select(x => new OvertimeBalanceSummaryDto
            {
                EmployeeId = x.EmployeeId,
                DisplayName = x.DisplayName,
                AsOfDate = x.WorkDate,
                RunningBalance = x.RunningBalance ?? 0m
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<OvertimeBalanceSummaryDto>> GetCurrentBalancesAsync(
        IReadOnlyCollection<int>? employeeIds, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.OvertimeBalanceDaily.AsNoTracking();
        if (employeeIds is { Count: > 0 })
        {
            query = query.Where(x => employeeIds.Contains(x.EmployeeId));
        }

        return await query
            .GroupBy(x => x.EmployeeId)
            .Select(g => g.OrderByDescending(x => x.WorkDate).First())
            .Select(x => new OvertimeBalanceSummaryDto
            {
                EmployeeId = x.EmployeeId,
                DisplayName = x.DisplayName,
                AsOfDate = x.WorkDate,
                RunningBalance = x.RunningBalance ?? 0m
            })
            .ToListAsync(cancellationToken);
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
                TotalBalance = g.Sum(x => x.RunningBalance ?? 0m),
                AverageBalance = g.Average(x => x.RunningBalance ?? 0m),
                EmployeeCount = g.Count()
            })
            .OrderBy(x => x.WorkDate)
            .ToListAsync(cancellationToken);
    }

    private static Expression<Func<VwOvertimeBalance, OvertimeBalanceDayDto>> MapToDtoExpression()
    {
        return x => new OvertimeBalanceDayDto
        {
            EmployeeId = x.EmployeeId,
            DisplayName = x.DisplayName,
            WorkDate = x.WorkDate,
            ActualHours = x.ActualHours ?? 0m,
            ExpectedHours = x.ExpectedHours ?? 0m,
            AdjustmentHours = x.AdjustmentHours ?? 0m,
            DailyDelta = x.DailyDelta ?? 0m,
            RunningBalance = x.RunningBalance ?? 0m
        };
    }
}
