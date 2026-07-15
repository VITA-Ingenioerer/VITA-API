using Microsoft.AspNetCore.Mvc;
using Vita.Planning.Application.DTOs;
using Vita.Planning.Application.Interfaces;

namespace Vita.Planning.Api.Controllers;

[ApiController]
[Route("api/vita-holidays")]
public sealed class VitaHolidaysController : ControllerBase
{
    private readonly IVitaHolidayService _service;

    public VitaHolidaysController(IVitaHolidayService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<VitaHolidayDto>>> GetAll(
        [FromQuery] string? countryCode,
        [FromQuery] DateOnly? fromDate,
        [FromQuery] DateOnly? toDate,
        CancellationToken cancellationToken)
    {
        var result = await _service.GetAllAsync(countryCode, fromDate, toDate, cancellationToken);
        return Ok(result);
    }
}
