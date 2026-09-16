using Microsoft.AspNetCore.Mvc;
using Vita.Atlas.Application.DTOs;
using Vita.Atlas.Application.Interfaces;

namespace Vita.Atlas.Api.Controllers;

[ApiController]
[Route("api/analytics")]
public sealed class AnalyticsController : ControllerBase
{
    private readonly ISalesAnalyticsService _service;

    public AnalyticsController(ISalesAnalyticsService service)
    {
        _service = service;
    }

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
}
