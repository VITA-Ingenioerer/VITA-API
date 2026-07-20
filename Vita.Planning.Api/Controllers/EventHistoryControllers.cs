using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vita.Planning.Application.Interfaces;

namespace Vita.Planning.Api.Controllers;

[ApiController]
[Route("api/history/resource-plan-entries")]
[Authorize(Policy = "PlannerAccess")]
public sealed class ResourcePlanEntryHistoryController : ControllerBase
{
    private readonly IResourcePlanEntryHistoryService _service;

    public ResourcePlanEntryHistoryController(IResourcePlanEntryHistoryService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> Query(
        [FromQuery] int? employeeId,
        [FromQuery] int? planningTargetId,
        [FromQuery] string? changeType,
        [FromQuery] string? changedByUserId,
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var result = await _service.QueryAsync(
            employeeId, planningTargetId, changeType, changedByUserId, fromUtc, toUtc, page, pageSize, cancellationToken);

        return Ok(result);
    }

    [HttpGet("by-entry/{entryId:int}")]
    public async Task<IActionResult> GetByEntry(int entryId, CancellationToken cancellationToken)
    {
        var result = await _service.GetByEntryIdAsync(entryId, cancellationToken);
        return Ok(result);
    }

    [HttpGet("by-employee/{employeeId:int}")]
    public async Task<IActionResult> GetByEmployee(
        int employeeId,
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        CancellationToken cancellationToken)
    {
        var result = await _service.GetByEmployeeAsync(employeeId, from, to, cancellationToken);
        return Ok(result);
    }

    [HttpGet("by-planning-target/{planningTargetId:int}")]
    public async Task<IActionResult> GetByPlanningTarget(
        int planningTargetId,
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        CancellationToken cancellationToken)
    {
        var result = await _service.GetByPlanningTargetAsync(planningTargetId, from, to, cancellationToken);
        return Ok(result);
    }

    [HttpGet("by-correlation/{correlationId:guid}")]
    public async Task<IActionResult> GetByCorrelation(Guid correlationId, CancellationToken cancellationToken)
    {
        var result = await _service.GetByCorrelationIdAsync(correlationId, cancellationToken);
        return Ok(result);
    }

    [HttpGet("by-actor/{userId}")]
    public async Task<IActionResult> GetByActor(
        string userId,
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        [FromQuery] int take,
        CancellationToken cancellationToken)
    {
        var result = await _service.GetByChangedByAsync(userId, fromUtc, toUtc, take <= 0 ? 200 : take, cancellationToken);
        return Ok(result);
    }
}

[ApiController]
[Route("api/history/business-events")]
[Authorize(Policy = "PlannerAccess")]
public sealed class BusinessEventsController : ControllerBase
{
    private readonly IBusinessEventService _service;

    public BusinessEventsController(IBusinessEventService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> Query(
        [FromQuery] string? entityType,
        [FromQuery] string? entityId,
        [FromQuery] int? planningTargetId,
        [FromQuery] string? eventType,
        [FromQuery] string? createdByUserId,
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var result = await _service.QueryAsync(
            entityType,
            entityId,
            planningTargetId,
            eventType,
            createdByUserId,
            fromUtc,
            toUtc,
            page,
            pageSize,
            cancellationToken);

        return Ok(result);
    }

    [HttpGet("by-entity")]
    public async Task<IActionResult> GetByEntity(
        [FromQuery] string entityType,
        [FromQuery] string entityId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(entityType) || string.IsNullOrWhiteSpace(entityId))
            return BadRequest("entityType and entityId are required.");

        var result = await _service.GetByEntityAsync(entityType, entityId, cancellationToken);
        return Ok(result);
    }

    [HttpGet("by-planning-target/{planningTargetId:int}")]
    public async Task<IActionResult> GetByPlanningTarget(int planningTargetId, CancellationToken cancellationToken)
    {
        var result = await _service.GetByPlanningTargetAsync(planningTargetId, cancellationToken);
        return Ok(result);
    }

    [HttpGet("by-correlation/{correlationId:guid}")]
    public async Task<IActionResult> GetByCorrelation(Guid correlationId, CancellationToken cancellationToken)
    {
        var result = await _service.GetByCorrelationIdAsync(correlationId, cancellationToken);
        return Ok(result);
    }
}

[ApiController]
[Route("api/history/sync-runs")]
[Authorize(Policy = "PlannerAccess")]
public sealed class SyncRunHistoryController : ControllerBase
{
    private readonly ISyncRunService _service;

    public SyncRunHistoryController(ISyncRunService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> Query(
        [FromQuery] string? sourceSystem,
        [FromQuery] string? status,
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var result = await _service.QueryRunsAsync(
            sourceSystem, status, fromUtc, toUtc, page, pageSize, cancellationToken);

        return Ok(result);
    }

    [HttpGet("{id:long}/errors")]
    public async Task<IActionResult> GetErrors(long id, CancellationToken cancellationToken)
    {
        var result = await _service.GetErrorsByRunIdAsync(id, cancellationToken);
        return Ok(result);
    }
}

[ApiController]
[Route("api/history/errors")]
[Authorize(Policy = "PlannerAccess")]
public sealed class OpsErrorsController : ControllerBase
{
    private readonly IErrorLogService _service;

    public OpsErrorsController(IErrorLogService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> Query(
        [FromQuery] Guid? correlationId,
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var result = await _service.QueryAsync(correlationId, fromUtc, toUtc, page, pageSize, cancellationToken);
        return Ok(result);
    }
}

[ApiController]
[Route("api/history/trace")]
[Authorize(Policy = "PlannerAccess")]
public sealed class CorrelationTraceController : ControllerBase
{
    private readonly ICorrelationTraceService _service;

    public CorrelationTraceController(ICorrelationTraceService service)
    {
        _service = service;
    }

    [HttpGet("{correlationId:guid}")]
    public async Task<IActionResult> Get(Guid correlationId, CancellationToken cancellationToken)
    {
        var result = await _service.GetTraceAsync(correlationId, cancellationToken);
        return Ok(result);
    }
}
