using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vita.Planning.Application.Interfaces;

namespace Vita.Planning.Api.Controllers;

/// <summary>
/// Logbook and point-in-time reconstruction over planned hours, built on top of
/// core.resource_plan_entry_history. No separate snapshot has to be taken in advance —
/// "as of" and "compare" work for any past moment because every hour change is already recorded.
/// </summary>
[ApiController]
[Route("api/resource-plan-history")]
[Authorize]
public sealed class ResourcePlanHistoryQueryController : ControllerBase
{
    private readonly IResourcePlanHistoryQueryService _service;

    public ResourcePlanHistoryQueryController(IResourcePlanHistoryQueryService service)
    {
        _service = service;
    }

    /// <summary>Who changed what, when — grouped into logical actions (one bulk save/auto-distribute = one entry).</summary>
    [HttpGet("logbook")]
    public async Task<IActionResult> GetLogbook(
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        [FromQuery] int? employeeId,
        [FromQuery] int? planningTargetId,
        [FromQuery] string? changedByUserId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var result = await _service.GetLogbookAsync(
            fromUtc, toUtc, employeeId, planningTargetId, changedByUserId, page, pageSize, cancellationToken);

        return Ok(result);
    }

    /// <summary>Reconstructs planned hours as they stood at a given moment, for a date range.</summary>
    [HttpGet("as-of")]
    public async Task<IActionResult> GetStateAsOf(
        [FromQuery] DateTime asOfUtc,
        [FromQuery] DateOnly periodFrom,
        [FromQuery] DateOnly periodTo,
        [FromQuery] int? employeeId,
        [FromQuery] int? scenarioId,
        CancellationToken cancellationToken)
    {
        if (periodTo < periodFrom)
            return BadRequest(new { message = "periodTo must be greater than or equal to periodFrom." });

        var result = await _service.GetStateAsOfAsync(asOfUtc, periodFrom, periodTo, employeeId, scenarioId, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Compares two "as of" reconstructions of the same period, e.g. "Maj 2027 as planned in
    /// March" vs "Maj 2027 as planned today". Omit employeeId for a company-wide comparison.
    /// </summary>
    [HttpGet("compare")]
    public async Task<IActionResult> Compare(
        [FromQuery] DateTime asOfFromUtc,
        [FromQuery] DateTime asOfToUtc,
        [FromQuery] DateOnly periodFrom,
        [FromQuery] DateOnly periodTo,
        [FromQuery] int? employeeId,
        [FromQuery] int? scenarioId,
        CancellationToken cancellationToken)
    {
        if (periodTo < periodFrom)
            return BadRequest(new { message = "periodTo must be greater than or equal to periodFrom." });

        var result = await _service.CompareAsOfAsync(
            asOfFromUtc, asOfToUtc, periodFrom, periodTo, employeeId, scenarioId, cancellationToken);

        return Ok(result);
    }
}
