using Microsoft.AspNetCore.Mvc;
using Vita.Atlas.Application.DTOs;
using Vita.Atlas.Application.Interfaces;

namespace Vita.Atlas.Api.Controllers;

[ApiController]
[Route("api/analytics")]
public sealed class AnalyticsController : ControllerBase
{
    private readonly ISalesAnalyticsService _service;
    private readonly IBacklogAnalyticsService _backlogService;

    public AnalyticsController(ISalesAnalyticsService service, IBacklogAnalyticsService backlogService)
    {
        _service = service;
        _backlogService = backlogService;
    }

    /// <summary>
    /// Order intake: offers only, as a flow over a period. Pair with backlog-overview, never
    /// summed with it — see BacklogAnalyticsDto.
    /// </summary>
    [HttpGet("sales-overview")]
    public async Task<ActionResult<SalesAnalyticsDto>> GetSalesOverview(
        [FromQuery] int? year,
        [FromQuery] string? officeCode,
        [FromQuery] string? region,
        CancellationToken cancellationToken)
    {
        var filter = new SalesAnalyticsFilterRequest { Year = year, OfficeCode = officeCode, Region = region };
        var result = await _service.GetSalesAnalyticsAsync(filter, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// The order book: projects only, as a stock at <c>asOfUtc</c>. No year parameter on purpose
    /// — a period filter on a balance is meaningless.
    /// </summary>
    [HttpGet("backlog-overview")]
    public async Task<ActionResult<BacklogAnalyticsDto>> GetBacklogOverview(
        [FromQuery] string? officeCode,
        [FromQuery] string? region,
        CancellationToken cancellationToken)
    {
        var filter = new BacklogAnalyticsFilterRequest { OfficeCode = officeCode, Region = region };
        var result = await _backlogService.GetBacklogAsync(filter, cancellationToken);
        return Ok(result);
    }
}
