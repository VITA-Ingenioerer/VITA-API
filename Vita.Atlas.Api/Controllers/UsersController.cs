using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Vita.Atlas.Application.DTOs;
using Vita.Atlas.Application.Interfaces;
using Vita.Atlas.Infrastructure.Data;

namespace Vita.Atlas.Api.Controllers;

[ApiController]
[Route("api/users")]
public sealed class UsersController : ControllerBase
{
    private readonly AtlasDbContext _dbContext;
    private readonly IUserSyncService _syncService;
    private readonly IOutOfOfficeCalendarService _outOfOfficeService;
    private readonly IEmployeeIdentityService _employeeIdentityService;
    private readonly IEntraUserProfileClient _entraUserProfileClient;

    public UsersController(
        AtlasDbContext dbContext,
        IUserSyncService syncService,
        IOutOfOfficeCalendarService outOfOfficeService,
        IEmployeeIdentityService employeeIdentityService,
        IEntraUserProfileClient entraUserProfileClient)
    {
        _dbContext = dbContext;
        _syncService = syncService;
        _outOfOfficeService = outOfOfficeService;
        _employeeIdentityService = employeeIdentityService;
        _entraUserProfileClient = entraUserProfileClient;
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
                u.SourceLastSyncedAt,
                u.Note,

                // Served from the local read model, not Graph: this endpoint returns the
                // whole roster and a Graph call per employee would make it unusable. Entra
                // stays the authority — the read model is refreshed on every classification
                // read/write and by the reconciliation job.
                u.PrimaryFaglighed,
                u.Profession,
                SecondaryFagligheder = _dbContext.UserSecondaryFagligheder
                    .AsNoTracking()
                    .Where(s => s.EmployeeId == u.EmployeeId)
                    .OrderBy(s => s.Faglighed)
                    .Select(s => s.Faglighed)
                    .ToList()
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
                u.SourceLastSyncedAt,
                u.Note,

                // Served from the local read model, not Graph: this endpoint returns the
                // whole roster and a Graph call per employee would make it unusable. Entra
                // stays the authority — the read model is refreshed on every classification
                // read/write and by the reconciliation job.
                u.PrimaryFaglighed,
                u.Profession,
                SecondaryFagligheder = _dbContext.UserSecondaryFagligheder
                    .AsNoTracking()
                    .Where(s => s.EmployeeId == u.EmployeeId)
                    .OrderBy(s => s.Faglighed)
                    .Select(s => s.Faglighed)
                    .ToList()
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (user is null)
        {
            return NotFound();
        }

        return Ok(user);
    }

    // Department, office and manager live in Entra — this writes them there first and only
    // mirrors the result into ext.users afterwards. Doing it the other way round would leave
    // our row "correct" and Entra stale until someone noticed, and the next user sync would
    // quietly overwrite our value from Entra anyway.
    [HttpPut("{employeeId:int}/profile")]
    public async Task<IActionResult> UpdateProfile(
        int employeeId,
        [FromBody] UpdateUserProfileRequest request,
        CancellationToken cancellationToken)
    {
        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.EmployeeId == employeeId, cancellationToken);

        if (user is null)
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(user.UserPrincipalName))
        {
            return Conflict(new { message = "Brugeren har ingen Entra-konto og kan ikke opdateres." });
        }

        string? managerUserPrincipalName = null;

        if (request.ManagerEmployeeId.HasValue)
        {
            var manager = await _dbContext.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.EmployeeId == request.ManagerEmployeeId.Value, cancellationToken);

            if (manager is null || string.IsNullOrWhiteSpace(manager.UserPrincipalName))
            {
                return BadRequest(new { message = $"Leder {request.ManagerEmployeeId.Value} blev ikke fundet i Entra." });
            }

            managerUserPrincipalName = manager.UserPrincipalName;
        }

        var department = string.IsNullOrWhiteSpace(request.Department) ? null : request.Department.Trim();
        var officeLocation = string.IsNullOrWhiteSpace(request.OfficeLocation) ? null : request.OfficeLocation.Trim();

        try
        {
            await _entraUserProfileClient.UpdateProfileAsync(
                user.UserPrincipalName, department, officeLocation, cancellationToken);

            // Only touched when the caller actually changed it: a manager write is a
            // directory relationship change, not worth replaying on every save.
            if (request.ManagerEmployeeId != user.ManagerEmployeeId)
            {
                await _entraUserProfileClient.UpdateManagerAsync(
                    user.UserPrincipalName, managerUserPrincipalName, cancellationToken);
            }
        }
        catch (InvalidOperationException ex)
        {
            // Nothing has been written locally at this point, so our row still matches Entra.
            return StatusCode(StatusCodes.Status502BadGateway, new { message = ex.Message });
        }

        user.Department = department;
        user.OfficeLocation = officeLocation;
        user.ManagerEmployeeId = request.ManagerEmployeeId;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new { user.EmployeeId, user.Department, user.OfficeLocation, user.ManagerEmployeeId });
    }

    // The note is the one thing on a user that is ours to write — everything else on the row
    // comes from e-conomic or Graph and is replaced on the next sync. Hence a field-specific
    // endpoint rather than a general user update that would invite editing synced columns.
    [HttpPut("{employeeId:int}/note")]
    public async Task<IActionResult> UpdateNote(
        int employeeId,
        [FromBody] UpdateUserNoteRequest request,
        CancellationToken cancellationToken)
    {
        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.EmployeeId == employeeId, cancellationToken);

        if (user is null)
        {
            return NotFound();
        }

        user.Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new { user.EmployeeId, user.Note });
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

    /// <summary>
    /// The user's faglighed/profession, straight from Microsoft Entra.
    /// </summary>
    [HttpGet("{employeeId:int}/classification")]
    public async Task<ActionResult<EmployeeClassificationDto>> GetClassification(
        int employeeId,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _employeeIdentityService.GetAsync(employeeId, cancellationToken));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { message = ex.Message });
        }
    }

    /// <summary>
    /// Writes the classification to Entra, mirrors it to extensionAttribute1-3 and refreshes
    /// the local read model. Restricted: these values can drive Entra dynamic group membership
    /// and downstream access, so ordinary planner users must not change them.
    /// </summary>
    [HttpPut("{employeeId:int}/classification")]
    [Authorize(Policy = "EmployeeClassificationWrite")]
    public async Task<ActionResult<EmployeeClassificationDto>> UpdateClassification(
        int employeeId,
        [FromBody] UpdateEmployeeClassificationRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            // The caller identifies the employee by id only. The UPN that Graph is called with
            // is resolved server-side from ext.users, never taken from the request.
            var caller = CallerInfo.FromClaimsPrincipal(User);

            return Ok(await _employeeIdentityService.UpdateAsync(employeeId, request, caller, cancellationToken));
        }
        catch (EmployeeClassificationValidationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { message = ex.Message });
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