using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Vita.Planning.Application.DTOs;
using Vita.Planning.Application.Interfaces;
using Vita.Planning.Infrastructure.Data;

namespace Vita.Planning.Api.Controllers;

[ApiController]
[Route("api/users")]
public sealed class UsersController : ControllerBase
{
    private readonly PlanningDbContext _dbContext;
    private readonly IUserSyncService _syncService;
    private readonly IOutOfOfficeCalendarService _outOfOfficeService;

    public UsersController(
        PlanningDbContext dbContext,
        IUserSyncService syncService,
        IOutOfOfficeCalendarService outOfOfficeService)
    {
        _dbContext = dbContext;
        _syncService = syncService;
        _outOfOfficeService = outOfOfficeService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var users = await _dbContext.Users
            .AsNoTracking()
            .Where(u => u.IsActive)
            .OrderBy(u => u.DisplayName)
            .Select(u => new
            {
                u.EmployeeId,
                u.UserPrincipalName,
                u.DisplayName,
                u.OfficeLocation,
                u.Department,
                u.EmployeeType,
                u.ManagerEmployeeId,
                Manager = _dbContext.Users
                    .AsNoTracking()
                    .Where(m => m.EmployeeId == u.ManagerEmployeeId)
                    .Select(m => new
                    {
                        m.EmployeeId,
                        m.UserPrincipalName,
                        m.DisplayName
                    })
                    .FirstOrDefault(),
                u.IsActive,
                u.SourceLastSyncedAt
            })
            .ToListAsync(cancellationToken);

        return Ok(users);
    }

    [HttpGet("{employeeId:int}")]
    public async Task<IActionResult> GetById(int employeeId, CancellationToken cancellationToken)
    {
        var user = await _dbContext.Users
            .AsNoTracking()
            .Where(u => u.EmployeeId == employeeId)
            .Select(u => new
            {
                u.EmployeeId,
                u.UserPrincipalName,
                u.DisplayName,
                u.OfficeLocation,
                u.Department,
                u.EmployeeType,
                u.ManagerEmployeeId,
                Manager = _dbContext.Users
                    .AsNoTracking()
                    .Where(m => m.EmployeeId == u.ManagerEmployeeId)
                    .Select(m => new
                    {
                        m.EmployeeId,
                        m.UserPrincipalName,
                        m.DisplayName
                    })
                    .FirstOrDefault(),
                u.IsActive,
                u.SourceLastSyncedAt
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (user is null)
        {
            return NotFound();
        }

        return Ok(user);
    }

    [HttpGet("{employeeId:int}/resource-plan")]
    public async Task<ActionResult<ResourcePlanDto>> GetResourcePlan(int employeeId, CancellationToken cancellationToken)
    {
        var resourcePlan = await _dbContext.ResourcePlans
            .AsNoTracking()
            .Where(x => x.EmployeeId == employeeId)
            .OrderByDescending(x => x.IsActive)
            .ThenByDescending(x => x.StartYear)
            .ThenByDescending(x => x.StartMonth)
            .ThenByDescending(x => x.ResourcePlanId)
            .Select(x => new ResourcePlanDto
            {
                ResourcePlanId = x.ResourcePlanId,
                EmployeeId = x.EmployeeId,
                ScenarioId = x.ScenarioId,
                StartYear = x.StartYear,
                StartMonth = x.StartMonth,
                VisibleMonths = x.VisibleMonths,
                Notes = x.Notes,
                IsActive = x.IsActive,
                CreatedBy = x.CreatedBy,
                UpdatedBy = x.UpdatedBy,
                CreatedAt = x.CreatedAt,
                UpdatedAt = x.UpdatedAt
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (resourcePlan is null)
        {
            return NotFound();
        }

        return Ok(resourcePlan);
    }

    [HttpPost("{employeeId:int}/calendar/out-of-office")]
    public async Task<ActionResult<OutOfOfficeCalendarEventDto>> CreateOutOfOffice(
        int employeeId,
        [FromBody] CreateOutOfOfficeCalendarEventRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var caller = CallerInfo.FromClaimsPrincipal(User);
            var result = await _outOfOfficeService.CreateAsync(employeeId, request, caller, cancellationToken);
            return StatusCode(StatusCodes.Status201Created, result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("sync")]
    public async Task<ActionResult<UserSyncResultDto>> Sync(CancellationToken cancellationToken)
    {
        var result = await _syncService.SyncUsersAsync(
            initiatedBy: "api",
            cancellationToken: cancellationToken);

        return Ok(result);
    }
}