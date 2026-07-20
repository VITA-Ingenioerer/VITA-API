using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vita.Planning.Application.DTOs;
using Vita.Planning.Application.Interfaces;

namespace Vita.Planning.Api.Controllers;

[ApiController]
[Route("api/project-lifecycle-log")]
[Authorize(Policy = "PlannerAccess")]
public sealed class ProjectLifecycleLogController : ControllerBase
{
    private readonly IProjectLifecycleLogService _service;

    public ProjectLifecycleLogController(IProjectLifecycleLogService service)
    {
        _service = service;
    }

    /// <summary>
    /// Get log entries, optionally filtered by targetType, projectNumber, offerId, or eventType.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PagedResultDto<ProjectLifecycleLogDto>>> GetAll(
        [FromQuery] string? targetType = null,
        [FromQuery] int? projectNumber = null,
        [FromQuery] int? offerId = null,
        [FromQuery] string? eventType = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var result = await _service.GetAllAsync(targetType, projectNumber, offerId, eventType, page, pageSize, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Get a single log entry by id.
    /// </summary>
    [HttpGet("{id:long}")]
    public async Task<ActionResult<ProjectLifecycleLogDto>> GetById(long id, CancellationToken cancellationToken)
    {
        var result = await _service.GetByIdAsync(id, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>
    /// Manually append a lifecycle log entry.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ProjectLifecycleLogDto>> Create(
        [FromBody] CreateProjectLifecycleLogRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _service.CreateAsync(request, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = result.ProjectLifecycleLogId }, result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
