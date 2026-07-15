using Microsoft.AspNetCore.Mvc;
using Vita.Planning.Application.DTOs;
using Vita.Planning.Application.Interfaces;

namespace Vita.Planning.Api.Controllers;

[ApiController]
[Route("api/project-metadata")]
public sealed class ProjectMetadataController : ControllerBase
{
    private readonly IProjectMetadataService _service;

    public ProjectMetadataController(IProjectMetadataService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ProjectMetadataDto>>> GetAll(CancellationToken cancellationToken)
    {
        var result = await _service.GetAllAsync(cancellationToken);
        return Ok(result);
    }

    [HttpGet("project/{projectNumber:int}")]
    public async Task<ActionResult<ProjectMetadataDto>> GetByProjectNumber(int projectNumber, CancellationToken cancellationToken)
    {
        var result = await _service.GetByProjectNumberAsync(projectNumber, cancellationToken);

        if (result is null)
            return NotFound();

        return Ok(result);
    }

    [HttpPut("project/{projectNumber:int}")]
    public async Task<ActionResult<ProjectMetadataDto>> UpsertForProject(
        int projectNumber,
        [FromBody] UpsertProjectMetadataRequest request,
        CancellationToken cancellationToken)
    {
        var caller = CallerInfo.FromClaimsPrincipal(User);
        var result = await _service.UpsertForProjectAsync(projectNumber, request, caller, cancellationToken);
        return Ok(result);
    }
}
