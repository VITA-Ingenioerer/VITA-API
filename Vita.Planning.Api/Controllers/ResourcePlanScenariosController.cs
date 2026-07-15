using Microsoft.AspNetCore.Mvc;
using Vita.Planning.Application.DTOs;
using Vita.Planning.Application.Interfaces;

namespace Vita.Planning.Api.Controllers;

[ApiController]
[Route("api/resource-plan-scenarios")]
public sealed class ResourcePlanScenariosController : ControllerBase
{
    private readonly IResourcePlanScenarioService _service;

    public ResourcePlanScenariosController(IResourcePlanScenarioService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ResourcePlanScenarioDto>>> GetAll(CancellationToken cancellationToken)
    {
        var result = await _service.GetAllAsync(cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ResourcePlanScenarioDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var result = await _service.GetByIdAsync(id, cancellationToken);

        if (result is null)
        {
            return NotFound();
        }

        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<ResourcePlanScenarioDto>> Create(
        [FromBody] CreateResourcePlanScenarioRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _service.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.ScenarioId }, result);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<ResourcePlanScenarioDto>> Update(
        int id,
        [FromBody] UpdateResourcePlanScenarioRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _service.UpdateAsync(id, request, cancellationToken);

        if (result is null)
        {
            return NotFound();
        }

        return Ok(result);
    }
}
