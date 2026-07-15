using Microsoft.AspNetCore.Mvc;
using Vita.Planning.Application.Interfaces;

namespace Vita.Planning.Api.Controllers;

[ApiController]
[Route("api/dawa")]
public sealed class DawaController : ControllerBase
{
    private readonly IDawaService _dawaService;

    public DawaController(IDawaService dawaService)
    {
        _dawaService = dawaService;
    }

    [HttpGet("search")]
    public async Task<IActionResult> Search(
        [FromQuery] string query,
        [FromQuery] string? postnr,
        [FromQuery] string? regionskode,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query))
            return BadRequest("query is required.");

        var results = await _dawaService.SearchAsync(query.Trim(), postnr, regionskode, cancellationToken);
        return Ok(results);
    }

    [HttpGet("postal-codes")]
    public async Task<IActionResult> SearchPostalCodes([FromQuery] string query, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query))
            return BadRequest("query is required.");

        var results = await _dawaService.SearchPostalCodesAsync(query.Trim(), cancellationToken);
        return Ok(results);
    }

    [HttpGet("regions")]
    public async Task<IActionResult> GetRegions(CancellationToken cancellationToken)
    {
        var results = await _dawaService.GetRegionsAsync(cancellationToken);
        return Ok(results);
    }
}
