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

    /// <summary>
    /// Latest running balance for multiple employees in one call. Omit employeeIds for every
    /// employee. Backs the company/team dashboard so it doesn't do one request per person.
    /// </summary>
    [HttpGet("current")]
    public async Task<ActionResult<IReadOnlyList<OvertimeBalanceSummaryDto>>> GetCurrentBatch(
        [FromQuery] int[]? employeeIds,
        CancellationToken cancellationToken)
    {
        var result = await _service.GetCurrentBalancesAsync(
            employeeIds is { Length: > 0 } ? employeeIds : null, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Daily total/average running balance across employees, for the aggregate trend chart.
    /// Aggregation happens in SQL so this scales to the whole company. Omit employeeIds for
    /// every employee.
    /// </summary>
    [HttpGet("trend")]
    public async Task<ActionResult<IReadOnlyList<OvertimeTrendPointDto>>> GetTrend(
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        [FromQuery] int[]? employeeIds,
        CancellationToken cancellationToken)
    {
        if (to < from)
        {
            return BadRequest(new { message = "'to' must be greater than or equal to 'from'." });
        }

        var result = await _service.GetTrendAsync(
            from, to, employeeIds is { Length: > 0 } ? employeeIds : null, cancellationToken);
        return Ok(result);
    }
}
