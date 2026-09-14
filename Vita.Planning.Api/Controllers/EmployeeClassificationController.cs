using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vita.Planning.Application.DTOs;
using Vita.Planning.Application.Interfaces;

namespace Vita.Planning.Api.Controllers;

/// <summary>
/// Read and write VITA employee classification (faglighed / profession). Microsoft Entra
/// is the authority; this API is the only path the frontend uses to reach it, so that
/// authorization, validation, Graph permissions and auditing all live in one place.
/// </summary>
[ApiController]
[Route("api")]
[Authorize(Policy = "PlannerAccess")]
public sealed class EmployeeClassificationController : ControllerBase
{
    private readonly IEmployeeIdentityService _employeeIdentityService;

    public EmployeeClassificationController(IEmployeeIdentityService employeeIdentityService)
    {
        _employeeIdentityService = employeeIdentityService;
    }

    /// <summary>
    /// The allowed values, read live from the Entra attribute definitions so adding a value
    /// in Entra needs no deploy. Readable by any authenticated planner user.
    /// </summary>
    [HttpGet("employee-classification/options")]
    public async Task<ActionResult<EmployeeClassificationOptionsDto>> GetOptions(
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _employeeIdentityService.GetOptionsAsync(cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { message = ex.Message });
        }
    }

    [HttpGet("employees/{employeeId:int}/classification")]
    public async Task<ActionResult<EmployeeClassificationDto>> Get(
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
    /// the local read model. Restricted: these values can drive Entra dynamic group
    /// membership and downstream access, so ordinary planner users must not change them.
    /// </summary>
    [HttpPut("employees/{employeeId:int}/classification")]
    [Authorize(Policy = "EmployeeClassificationWrite")]
    public async Task<ActionResult<EmployeeClassificationDto>> Update(
        int employeeId,
        [FromBody] UpdateEmployeeClassificationRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            // The caller identifies the employee by id only. The UPN that Graph is called
            // with is resolved server-side from ext.users, never taken from the request.
            var caller = CallerInfo.FromClaimsPrincipal(User);

            var result = await _employeeIdentityService.UpdateAsync(
                employeeId,
                request,
                caller,
                cancellationToken);

            return Ok(result);
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

    /// <summary>
    /// Pulls every classification from Entra into the local read model, for changes made
    /// directly in Entra. Read-only with respect to Entra — it never writes back.
    /// </summary>
    [HttpPost("employee-classification/reconcile")]
    [Authorize(Policy = "EmployeeClassificationWrite")]
    public async Task<IActionResult> Reconcile(CancellationToken cancellationToken)
    {
        try
        {
            var updated = await _employeeIdentityService.ReconcileAllAsync(cancellationToken);

            return Ok(new { updated });
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { message = ex.Message });
        }
    }
}
