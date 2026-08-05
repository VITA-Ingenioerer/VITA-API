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
