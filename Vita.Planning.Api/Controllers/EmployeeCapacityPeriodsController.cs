using Microsoft.AspNetCore.Mvc;
using Vita.Planning.Application.DTOs;
using Vita.Planning.Application.Interfaces;

namespace Vita.Planning.Api.Controllers;

[ApiController]
[Route("api/employee-capacity-periods")]
public sealed class EmployeeCapacityPeriodsController : ControllerBase
{
    private readonly IEmployeeCapacityPeriodService _service;
    private readonly IBusinessEventService _events;

    public EmployeeCapacityPeriodsController(IEmployeeCapacityPeriodService service, IBusinessEventService events)
    {
        _service = service;
        _events = events;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<EmployeeCapacityPeriodDto>>> GetAll(CancellationToken cancellationToken)
    {
        var result = await _service.GetAllAsync(cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<EmployeeCapacityPeriodDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var result = await _service.GetByIdAsync(id, cancellationToken);

        if (result is null)
        {
            return NotFound();
        }

        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<EmployeeCapacityPeriodDto>> Create(
        [FromBody] CreateEmployeeCapacityPeriodRequest request,
        CancellationToken cancellationToken)
    {
        var caller = CallerInfo.FromClaimsPrincipal(User);
        var result = await _service.CreateAsync(request, cancellationToken);

        await _events.RecordAsync(new RecordBusinessEventRequest
        {
            EventType = "CapacityChanged",
            EventTitle = $"Kapacitet oprettet: medarbejder {result.EmployeeId}",
            EntityType = "EmployeeCapacityPeriod",
            EntityId = result.EmployeeCapacityPeriodId.ToString(),
            NewValue = $"{result.WeeklyHoursBasis} timer/uge ({result.PeriodStart} – {result.PeriodEnd})",
            CreatedByUserId = caller.UserId,
            CreatedByName = caller.Name,
            SourceModule = "EmployeeCapacityPeriodsController"
        }, cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = result.EmployeeCapacityPeriodId }, result);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<EmployeeCapacityPeriodDto>> Update(
        int id,
        [FromBody] UpdateEmployeeCapacityPeriodRequest request,
        CancellationToken cancellationToken)
    {
        var caller = CallerInfo.FromClaimsPrincipal(User);
        var before = await _service.GetByIdAsync(id, cancellationToken);
        var result = await _service.UpdateAsync(id, request, cancellationToken);

        if (result is null)
        {
            return NotFound();
        }

        await _events.RecordAsync(new RecordBusinessEventRequest
        {
            EventType = "CapacityChanged",
            EventTitle = $"Kapacitet opdateret: medarbejder {result.EmployeeId}",
            EntityType = "EmployeeCapacityPeriod",
            EntityId = result.EmployeeCapacityPeriodId.ToString(),
            OldValue = before is not null ? $"{before.WeeklyHoursBasis} timer/uge" : null,
            NewValue = $"{result.WeeklyHoursBasis} timer/uge ({result.PeriodStart} – {result.PeriodEnd})",
            CreatedByUserId = caller.UserId,
            CreatedByName = caller.Name,
            SourceModule = "EmployeeCapacityPeriodsController"
        }, cancellationToken);

        return Ok(result);
    }
}
