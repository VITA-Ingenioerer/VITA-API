using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Vita.Planning.Application.DTOs;
using Vita.Planning.Infrastructure.Data;

namespace Vita.Planning.Api.Controllers;

[ApiController]
[Route("api/activities")]
public sealed class ActivitiesController : ControllerBase
{
    private readonly PlanningDbContext _dbContext;

    public ActivitiesController(PlanningDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ActivityDto>>> GetAll(CancellationToken cancellationToken)
    {
        var activities = await _dbContext.Activities
            .AsNoTracking()
            .OrderBy(a => a.Name)
            .ThenBy(a => a.ActivityNumber)
            .Select(a => new ActivityDto
            {
                ActivityNumber = a.ActivityNumber,
                ActivityGroupNumber = a.ActivityGroupNumber,
                Name = a.Name,
                CostPriceMarkupPercentage = a.CostPriceMarkupPercentage,
                CutoffDate = a.CutoffDate,
                HideInSearch = a.HideInSearch,
                InLieuCode = a.InLieuCode,
                IsAccessible = a.IsAccessible,
                SalesPriceAfter = a.SalesPriceAfter,
                SalesPriceBefore = a.SalesPriceBefore,
                ObjectVersion = a.ObjectVersion,
                SourceLastSyncedAt = a.SourceLastSyncedAt
            })
            .ToListAsync(cancellationToken);

        return Ok(activities);
    }

    [HttpGet("{activityNumber:int}")]
    public async Task<ActionResult<ActivityDto>> GetById(int activityNumber, CancellationToken cancellationToken)
    {
        var activity = await _dbContext.Activities
            .AsNoTracking()
            .Where(a => a.ActivityNumber == activityNumber)
            .Select(a => new ActivityDto
            {
                ActivityNumber = a.ActivityNumber,
                ActivityGroupNumber = a.ActivityGroupNumber,
                Name = a.Name,
                CostPriceMarkupPercentage = a.CostPriceMarkupPercentage,
                CutoffDate = a.CutoffDate,
                HideInSearch = a.HideInSearch,
                InLieuCode = a.InLieuCode,
                IsAccessible = a.IsAccessible,
                SalesPriceAfter = a.SalesPriceAfter,
                SalesPriceBefore = a.SalesPriceBefore,
                ObjectVersion = a.ObjectVersion,
                SourceLastSyncedAt = a.SourceLastSyncedAt
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (activity is null)
        {
            return NotFound();
        }

        return Ok(activity);
    }
}
