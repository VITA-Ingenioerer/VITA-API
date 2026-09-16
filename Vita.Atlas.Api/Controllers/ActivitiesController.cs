using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Vita.Atlas.Application.DTOs;
using Vita.Atlas.Infrastructure.Data;

namespace Vita.Atlas.Api.Controllers;

[ApiController]
[Route("api/activities")]
public sealed class ActivitiesController : ControllerBase
{
    private readonly AtlasDbContext _dbContext;

    public ActivitiesController(AtlasDbContext dbContext)
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
