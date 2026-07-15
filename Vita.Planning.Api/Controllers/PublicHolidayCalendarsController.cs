using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Json;
using Vita.Planning.Application.DTOs;
using Vita.Planning.Application.Interfaces;

namespace Vita.Planning.Api.Controllers;

[ApiController]
[Route("api/public-holiday-calendars")]
public sealed class PublicHolidayCalendarsController : ControllerBase
{
    private readonly IPublicHolidayCalendarService _service;
    private readonly IHttpClientFactory _httpClientFactory;

    public PublicHolidayCalendarsController(IPublicHolidayCalendarService service, IHttpClientFactory httpClientFactory)
    {
        _service = service;
        _httpClientFactory = httpClientFactory;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PublicHolidayCalendarDto>>> GetAll(CancellationToken cancellationToken)
    {
        var result = await _service.GetAllAsync(cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<PublicHolidayCalendarDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var result = await _service.GetByIdAsync(id, cancellationToken);

        if (result is null)
        {
            return NotFound();
        }

        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<PublicHolidayCalendarDto>> Create(
        [FromBody] CreatePublicHolidayCalendarRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _service.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.PublicHolidayCalendarId }, result);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<PublicHolidayCalendarDto>> Update(
        int id,
        [FromBody] UpdatePublicHolidayCalendarRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _service.UpdateAsync(id, request, cancellationToken);

        if (result is null)
        {
            return NotFound();
        }

        return Ok(result);
    }

    [HttpPost("sync")]
    public async Task<IActionResult> Sync(
        [FromQuery] string countryCode,
        [FromQuery] int year,
        CancellationToken cancellationToken)
    {
        try
        {
            var normalizedCountryCode = countryCode.Trim().ToUpperInvariant();
            var client = _httpClientFactory.CreateClient("NagerDate");
            var sourceHolidays = await client.GetFromJsonAsync<List<NagerPublicHolidayDto>>(
                $"api/v3/PublicHolidays/{year}/{normalizedCountryCode}",
                cancellationToken) ?? [];

            var holidays = sourceHolidays
                .Select(x => new PublicHolidayCalendarDto
                {
                    CountryCode = normalizedCountryCode,
                    HolidayDate = DateOnly.FromDateTime(x.Date.Date),
                    HolidayName = string.IsNullOrWhiteSpace(x.LocalName) ? x.Name : x.LocalName,
                    IsPublicHoliday = true,
                    IsHalfDay = false,
                    HoursReduction = null
                })
                .ToList();

            var result = await _service.SyncAsync(normalizedCountryCode, year, holidays, cancellationToken);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    private sealed class NagerPublicHolidayDto
    {
        public DateTime Date { get; set; }
        public string LocalName { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }
}
