using Microsoft.AspNetCore.Mvc;
using Vita.Planning.Application.DTOs;
using Vita.Planning.Application.Interfaces;

namespace Vita.Planning.Api.Controllers;

[ApiController]
[Route("api/overtime-adjustments")]
public sealed class OvertimeAdjustmentsController : ControllerBase
{
    private readonly IOvertimeAdjustmentService _service;

    public OvertimeAdjustmentsController(IOvertimeAdjustmentService service)
    {
        _service = service;
    }

    [HttpGet("{employeeId:int}")]
    public async Task<ActionResult<IReadOnlyList<OvertimeAdjustmentDto>>> GetForEmployee(
        int employeeId,
        CancellationToken cancellationToken)
    {
        var result = await _service.GetForEmployeeAsync(employeeId, cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<OvertimeAdjustmentDto>> Create(
        [FromBody] CreateOvertimeAdjustmentRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _service.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetForEmployee), new { employeeId = result.EmployeeId }, result);
    }
}
