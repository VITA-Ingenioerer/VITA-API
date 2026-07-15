using Microsoft.AspNetCore.Mvc;
using Vita.Planning.Application.Interfaces;

namespace Vita.Planning.Api.Controllers;

[ApiController]
[Route("api/virk")]
public sealed class VirkController : ControllerBase
{
    private readonly IVirkService _virkService;

    public VirkController(IVirkService virkService)
    {
        _virkService = virkService;
    }

    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string query, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return BadRequest("query is required.");
        }

        var result = await _virkService.SearchCompaniesAsync(query, cancellationToken);
        return Ok(result);
    }

    [HttpGet("company/{cvrNumber}")]
    public async Task<IActionResult> GetCompany(string cvrNumber, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(cvrNumber))
        {
            return BadRequest("cvrNumber is required.");
        }

        var result = await _virkService.GetCompanyAsync(cvrNumber, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }
}
