using Microsoft.AspNetCore.Mvc;
using Vita.Planning.Application.DTOs;
using Vita.Planning.Application.Interfaces;

namespace Vita.Planning.Api.Controllers;

[ApiController]
[Route("api/absence-registrations")]
public sealed class AbsenceController : ControllerBase
{
    private readonly IAbsenceRegistrationService _service;

    public AbsenceController(IAbsenceRegistrationService service)
    {
        _service = service;
    }

    /// <summary>
    /// Returns e-conomic time registrations booked against the absence ("Fravær") project
    /// within [fromDate, toDate] — actual bookings, not the resource-plan forecast.
    /// Optionally narrowed to one employee.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AbsenceRegistrationDto>>> GetAll(
        [FromQuery] DateOnly fromDate,
        [FromQuery] DateOnly toDate,
        [FromQuery] int? employeeId,
        CancellationToken cancellationToken)
    {
        if (toDate < fromDate)
        {
            return BadRequest(new { message = "toDate must be on or after fromDate." });
        }

        var result = await _service.GetAbsenceRegistrationsAsync(fromDate, toDate, employeeId, cancellationToken);
        return Ok(result);
    }
}
