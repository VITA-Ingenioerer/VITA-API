using Microsoft.AspNetCore.Mvc;
using Vita.Planning.Application.DTOs;
using Vita.Planning.Application.Interfaces;

namespace Vita.Planning.Api.Controllers;

[ApiController]
[Route("api/vita-holiday-overrides")]
public sealed class VitaHolidayOverridesController : ControllerBase
{
    private readonly IVitaHolidayService _service;
    private readonly IBusinessEventService _events;

    public VitaHolidayOverridesController(IVitaHolidayService service, IBusinessEventService events)
    {
        _service = service;
        _events = events;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<VitaHolidayOverrideDto>>> GetAll(
        [FromQuery] string? countryCode,
        [FromQuery] DateOnly? fromDate,
        [FromQuery] DateOnly? toDate,
        CancellationToken cancellationToken)
    {
        var result = await _service.GetOverridesAsync(countryCode, fromDate, toDate, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<VitaHolidayOverrideDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var result = await _service.GetOverrideByIdAsync(id, cancellationToken);

        if (result is null)
        {
            return NotFound();
        }

        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<VitaHolidayOverrideDto>> Create(
        [FromBody] CreateVitaHolidayOverrideRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var caller = CallerInfo.FromClaimsPrincipal(User);
            var result = await _service.CreateOverrideAsync(request, cancellationToken);

            await _events.RecordAsync(new RecordBusinessEventRequest
            {
                EventType = "HolidayChanged",
                EventTitle = $"Helligdag tilføjet: {result.HolidayDate}",
                EntityType = "VitaHolidayOverride",
                EntityId = result.VitaHolidayOverrideId.ToString(),
                NewValue = $"{result.HolidayName} ({result.HolidayDate})",
                CreatedByUserId = caller.UserId,
                CreatedByName = caller.Name,
                SourceModule = "VitaHolidayOverridesController"
            }, cancellationToken);

            return CreatedAtAction(nameof(GetById), new { id = result.VitaHolidayOverrideId }, result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<VitaHolidayOverrideDto>> Update(
        int id,
        [FromBody] UpdateVitaHolidayOverrideRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var caller = CallerInfo.FromClaimsPrincipal(User);
            var result = await _service.UpdateOverrideAsync(id, request, cancellationToken);

            if (result is null)
            {
                return NotFound();
            }

            await _events.RecordAsync(new RecordBusinessEventRequest
            {
                EventType = "HolidayChanged",
                EventTitle = $"Helligdag opdateret: {result.HolidayDate}",
                EntityType = "VitaHolidayOverride",
                EntityId = result.VitaHolidayOverrideId.ToString(),
                NewValue = $"{result.HolidayName} ({result.HolidayDate})",
                CreatedByUserId = caller.UserId,
                CreatedByName = caller.Name,
                SourceModule = "VitaHolidayOverridesController"
            }, cancellationToken);

            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var result = await _service.DeleteOverrideAsync(id, cancellationToken);

        if (result is null)
        {
            return NotFound();
        }

        var caller = CallerInfo.FromClaimsPrincipal(User);
        await _events.RecordAsync(new RecordBusinessEventRequest
        {
            EventType = "HolidayChanged",
            EventTitle = $"Helligdag slettet: {result.HolidayDate}",
            EntityType = "VitaHolidayOverride",
            EntityId = result.VitaHolidayOverrideId.ToString(),
            NewValue = $"{result.HolidayName} ({result.HolidayDate})",
            CreatedByUserId = caller.UserId,
            CreatedByName = caller.Name,
            SourceModule = "VitaHolidayOverridesController"
        }, cancellationToken);

        return NoContent();
    }
}
