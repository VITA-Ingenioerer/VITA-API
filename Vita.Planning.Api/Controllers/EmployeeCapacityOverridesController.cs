using Microsoft.AspNetCore.Mvc;
using Vita.Planning.Application.DTOs;
using Vita.Planning.Application.Interfaces;

namespace Vita.Planning.Api.Controllers;

[ApiController]
[Route("api/employee-capacity-overrides")]
public sealed class EmployeeCapacityOverridesController : ControllerBase
{
    private readonly IEmployeeCapacityOverrideService _service;
    private readonly IBusinessEventService _events;

    public EmployeeCapacityOverridesController(IEmployeeCapacityOverrideService service, IBusinessEventService events)
    {
        _service = service;
        _events = events;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<EmployeeCapacityOverrideDto>>> GetAll(CancellationToken cancellationToken)
    {
        var result = await _service.GetAllAsync(cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<EmployeeCapacityOverrideDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var result = await _service.GetByIdAsync(id, cancellationToken);

        if (result is null)
        {
            return NotFound();
        }

        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<EmployeeCapacityOverrideDto>> Create(
        [FromBody] CreateEmployeeCapacityOverrideRequest request,
        CancellationToken cancellationToken)
    {
        var caller = CallerInfo.FromClaimsPrincipal(User);
        request.CreatedBy = caller.UserId ?? caller.Email ?? caller.Name;

        var result = await _service.CreateAsync(request, cancellationToken);

        await _events.RecordAsync(new RecordBusinessEventRequest
        {
            EventType = "CapacityOverrideChanged",
            EventTitle = $"Kapacitetsundtagelse oprettet: medarbejder {result.EmployeeId} fra {result.EffectiveFrom}",
            EntityType = "EmployeeCapacityOverride",
            EntityId = result.EmployeeCapacityOverrideId.ToString(),
            NewValue = $"{result.OverrideType} fra {result.EffectiveFrom}",
            CreatedByUserId = caller.UserId,
            CreatedByName = caller.Name,
            SourceModule = "EmployeeCapacityOverridesController"
        }, cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = result.EmployeeCapacityOverrideId }, result);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<EmployeeCapacityOverrideDto>> Update(
        int id,
        [FromBody] UpdateEmployeeCapacityOverrideRequest request,
        CancellationToken cancellationToken)
    {
        var caller = CallerInfo.FromClaimsPrincipal(User);
        request.UpdatedBy = caller.UserId ?? caller.Email ?? caller.Name;

        var result = await _service.UpdateAsync(id, request, cancellationToken);

        if (result is null)
        {
            return NotFound();
        }

        await _events.RecordAsync(new RecordBusinessEventRequest
        {
            EventType = "CapacityOverrideChanged",
            EventTitle = $"Kapacitetsundtagelse opdateret: medarbejder {result.EmployeeId} fra {result.EffectiveFrom}",
            EntityType = "EmployeeCapacityOverride",
            EntityId = result.EmployeeCapacityOverrideId.ToString(),
            NewValue = $"{result.OverrideType} fra {result.EffectiveFrom}",
            CreatedByUserId = caller.UserId,
            CreatedByName = caller.Name,
            SourceModule = "EmployeeCapacityOverridesController"
        }, cancellationToken);

        return Ok(result);
    }
}
