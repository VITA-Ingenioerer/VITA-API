using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vita.Planning.Application.DTOs;
using Vita.Planning.Application.Interfaces;

namespace Vita.Planning.Api.Controllers;

[ApiController]
[Route("api/resource-plan-entries")]
public sealed class ResourcePlanEntriesController : ControllerBase
{
    private readonly IResourcePlanEntryService _service;

    public ResourcePlanEntriesController(IResourcePlanEntryService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ResourcePlanEntryDto>>> GetAll(
        [FromQuery] int? yearNumber,
        [FromQuery] DateOnly? fromDate,
        [FromQuery] DateOnly? toDate,
        [FromQuery] int? employeeId,
        [FromQuery] int? scenarioId,
        [FromQuery] int? planningTargetId,
        [FromQuery] int? resourcePlanId,
        [FromQuery] int? virtualResourceId,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _service.GetAllAsync(
                yearNumber, fromDate, toDate, employeeId, scenarioId, planningTargetId, resourcePlanId, virtualResourceId, cancellationToken);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ResourcePlanEntryDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var result = await _service.GetByIdAsync(id, cancellationToken);

        if (result is null)
        {
            return NotFound();
        }

        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<ResourcePlanEntryDto>> Create(
        [FromBody] CreateResourcePlanEntryRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var caller = CallerInfo.FromClaimsPrincipal(User);
            var result = await _service.CreateAsync(request, caller, cancellationToken);

            return CreatedAtAction(
                nameof(GetById),
                new { id = result.ResourcePlanEntryId },
                result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateException ex)
            when (ex.InnerException?.Message.Contains("UX_core_resource_plan_entries_day") == true)
        {
            return Conflict(new { message = "An entry for this plan date already exists. Use the update endpoint to change hours." });
        }
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<ResourcePlanEntryDto>> Update(
        int id,
        [FromBody] UpdateResourcePlanEntryRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var caller = CallerInfo.FromClaimsPrincipal(User);
            var result = await _service.UpdateAsync(id, request, caller, cancellationToken);

            if (result is null)
            {
                return NotFound();
            }

            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("bulk")]
    public async Task<ActionResult<BulkUpsertResourcePlanEntriesResult>> SavePeriod(
        [FromBody] SaveResourcePlanEntriesPeriodRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var caller = CallerInfo.FromClaimsPrincipal(User);
            var result = await _service.SavePeriodAsync(request, caller, cancellationToken);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("bulk/auto-distribute")]
    public async Task<ActionResult<BulkUpsertResourcePlanEntriesResult>> AutoDistribute(
        [FromBody] AutoDistributeResourcePlanEntriesRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var caller = CallerInfo.FromClaimsPrincipal(User);
            var result = await _service.AutoDistributeAsync(request, caller, cancellationToken);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
