using Microsoft.AspNetCore.Mvc;
using Vita.Planning.Application.DTOs;
using Vita.Planning.Application.Interfaces;

namespace Vita.Planning.Api.Controllers;

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
    public async Task<ActionResult<SalesAnalyticsDto>> GetSalesOverview(CancellationToken cancellationToken)
    {
        var result = await _service.GetSalesAnalyticsAsync(cancellationToken);
        return Ok(result);
    }
}
