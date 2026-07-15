using Microsoft.AspNetCore.Mvc;
using Vita.Planning.Application.DTOs;
using Vita.Planning.Application.Interfaces;

namespace Vita.Planning.Api.Controllers;

[ApiController]
[Route("api/employee-capacity-profiles")]
public sealed class EmployeeCapacityProfilesController : ControllerBase
{
    private readonly IEmployeeCapacityProfileService _service;
    private readonly IBusinessEventService _events;

    public EmployeeCapacityProfilesController(IEmployeeCapacityProfileService service, IBusinessEventService events)
    {
        _service = service;
        _events = events;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<EmployeeCapacityProfileDto>>> GetAll(CancellationToken cancellationToken)
    {
        var result = await _service.GetAllAsync(cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<EmployeeCapacityProfileDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var result = await _service.GetByIdAsync(id, cancellationToken);

        if (result is null)
        {
            return NotFound();
        }

        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<EmployeeCapacityProfileDto>> Create(
        [FromBody] CreateEmployeeCapacityProfileRequest request,
        CancellationToken cancellationToken)
    {
        var caller = CallerInfo.FromClaimsPrincipal(User);
        request.CreatedBy = caller.UserId ?? caller.Email ?? caller.Name;

        var result = await _service.CreateAsync(request, cancellationToken);

        await _events.RecordAsync(new RecordBusinessEventRequest
        {
            EventType = "CapacityProfileChanged",
            EventTitle = $"Kapacitetsprofil oprettet: medarbejder {result.EmployeeId} fra {result.EffectiveFrom}",
            EntityType = "EmployeeCapacityProfile",
            EntityId = result.EmployeeCapacityProfileId.ToString(),
            NewValue = $"{result.DefaultWeeklyHours}t/uge fra {result.EffectiveFrom}",
            CreatedByUserId = caller.UserId,
            CreatedByName = caller.Name,
            SourceModule = "EmployeeCapacityProfilesController"
        }, cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = result.EmployeeCapacityProfileId }, result);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<EmployeeCapacityProfileDto>> Update(
        int id,
        [FromBody] UpdateEmployeeCapacityProfileRequest request,
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
            EventType = "CapacityProfileChanged",
            EventTitle = $"Kapacitetsprofil opdateret: medarbejder {result.EmployeeId} fra {result.EffectiveFrom}",
            EntityType = "EmployeeCapacityProfile",
            EntityId = result.EmployeeCapacityProfileId.ToString(),
            NewValue = $"{result.DefaultWeeklyHours}t/uge fra {result.EffectiveFrom}",
            CreatedByUserId = caller.UserId,
            CreatedByName = caller.Name,
            SourceModule = "EmployeeCapacityProfilesController"
        }, cancellationToken);

        return Ok(result);
    }
}
