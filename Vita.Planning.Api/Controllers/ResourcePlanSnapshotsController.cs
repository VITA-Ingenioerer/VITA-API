using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vita.Planning.Application.DTOs;
using Vita.Planning.Application.Interfaces;

namespace Vita.Planning.Api.Controllers;

[ApiController]
[Route("api/resource-plan-snapshots")]
[Authorize]
public sealed class ResourcePlanSnapshotsController : ControllerBase
{
    private readonly IResourcePlanSnapshotService _service;

    public ResourcePlanSnapshotsController(IResourcePlanSnapshotService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ResourcePlanSnapshotDto>>> GetAll(
        [FromQuery] int? scenarioId,
        CancellationToken cancellationToken)
    {
        var result = await _service.GetAllAsync(scenarioId, cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<ResourcePlanSnapshotDto>> Create(
        [FromBody] CreateResourcePlanSnapshotRequest request,
        CancellationToken cancellationToken)
    {
        var caller = CallerInfo.FromClaimsPrincipal(User);
        var result = await _service.CreateAsync(request, caller, cancellationToken);
        return CreatedAtAction(nameof(GetEntries), new { snapshotId = result.ResourcePlanSnapshotId }, result);
    }

    [HttpGet("{snapshotId:long}/entries")]
    public async Task<ActionResult<IReadOnlyList<ResourcePlanSnapshotEntryDto>>> GetEntries(
        long snapshotId,
        [FromQuery] int? employeeId,
        [FromQuery] int? planningTargetId,
        CancellationToken cancellationToken)
    {
        var result = await _service.GetEntriesAsync(snapshotId, employeeId, planningTargetId, cancellationToken);
        return Ok(result);
    }

    [HttpGet("compare")]
    public async Task<ActionResult<ResourcePlanSnapshotComparisonDto>> Compare(
        [FromQuery] long fromSnapshotId,
        [FromQuery] long toSnapshotId,
        [FromQuery] int? employeeId,
        [FromQuery] int? planningTargetId,
        CancellationToken cancellationToken)
    {
        if (fromSnapshotId <= 0 || toSnapshotId <= 0)
        {
            return BadRequest(new { message = "fromSnapshotId and toSnapshotId are required." });
        }

        var result = await _service.CompareAsync(
            fromSnapshotId,
            toSnapshotId,
            employeeId,
            planningTargetId,
            cancellationToken);

        return Ok(result);
    }
}
