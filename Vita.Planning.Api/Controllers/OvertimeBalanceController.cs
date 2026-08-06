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
    /// Latest running balance for multiple employees in one call. Omit employeeIds (or the
    /// whole body) for every employee. Backs the company/team dashboard so it doesn't do one
    /// request per person. POST, not GET: a few hundred employeeIds as repeated query params
    /// produces a URL long enough that Azure's front end rejects it before the request ever
    /// reaches this controller.
    /// </summary>
    [HttpPost("current")]
    public async Task<ActionResult<IReadOnlyList<OvertimeBalanceSummaryDto>>> GetCurrentBatch(
        [FromBody] EmployeeIdsFilterRequest? request,
        CancellationToken cancellationToken)
    {
        var result = await _service.GetCurrentBalancesAsync(
            request?.EmployeeIds is { Length: > 0 } ? request.EmployeeIds : null, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Daily total/average running balance across employees, for the aggregate trend chart.
    /// Aggregation happens in SQL so this scales to the whole company. Omit employeeIds (or
    /// the whole body) for every employee. POST for the same reason as GetCurrentBatch above.
    /// </summary>
    [HttpPost("trend")]
    public async Task<ActionResult<IReadOnlyList<OvertimeTrendPointDto>>> GetTrend(
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        [FromBody] EmployeeIdsFilterRequest? request,
        CancellationToken cancellationToken)
    {
        if (to < from)
        {
            return BadRequest(new { message = "'to' must be greater than or equal to 'from'." });
        }

        var result = await _service.GetTrendAsync(
            from, to, request?.EmployeeIds is { Length: > 0 } ? request.EmployeeIds : null, cancellationToken);
        return Ok(result);
    }
}
