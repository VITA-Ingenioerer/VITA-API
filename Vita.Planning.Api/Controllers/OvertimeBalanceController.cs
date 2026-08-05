using Microsoft.AspNetCore.Mvc;
using Vita.Planning.Application.DTOs;
using Vita.Planning.Application.Interfaces;

namespace Vita.Planning.Api.Controllers;

[ApiController]
[Route("api/overtime-balance")]
public sealed class OvertimeBalanceController : ControllerBase
{
    private readonly IOvertimeBalanceQueryService _service;

    public OvertimeBalanceController(IOvertimeBalanceQueryService service)
    {
        _service = service;
    }

    /// <summary>
    /// Daily actual-vs-expected hours plus adjustments and running balance, for one
    /// employee over a date range. Backed by core.vw_overtime_balance.
    /// </summary>
    [HttpGet("{employeeId:int}")]
    public async Task<ActionResult<IReadOnlyList<OvertimeBalanceDayDto>>> GetDaily(
        int employeeId,
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        CancellationToken cancellationToken)
    {
        if (to < from)
        {
            return BadRequest(new { message = "'to' must be greater than or equal to 'from'." });
        }

        var result = await _service.GetDailyBalanceAsync(employeeId, from, to, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// The employee's latest running balance as of their most recent tracked day.
    /// </summary>
    [HttpGet("{employeeId:int}/current")]
    public async Task<ActionResult<OvertimeBalanceSummaryDto>> GetCurrent(
        int employeeId,
        CancellationToken cancellationToken)
    {
        var result = await _service.GetCurrentBalanceAsync(employeeId, cancellationToken);

        if (result is null)
        {
            return NotFound();
        }

        return Ok(result);
    }
}
