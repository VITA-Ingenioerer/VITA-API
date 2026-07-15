using Microsoft.AspNetCore.Mvc;
using Vita.Planning.Application.DTOs;
using Vita.Planning.Application.Interfaces;

namespace Vita.Planning.Api.Controllers;

[ApiController]
[Route("api/employee-capacity-schedule")]
public sealed class EmployeeCapacityScheduleController : ControllerBase
{
    private readonly ICapacityScheduleQueryService _scheduleQueryService;

    public EmployeeCapacityScheduleController(ICapacityScheduleQueryService scheduleQueryService)
    {
        _scheduleQueryService = scheduleQueryService;
    }

    /// <summary>
    /// The employee's default (contracted) hours per day, ignoring any overrides.
    /// For time registration — always what the employee was hired for.
    /// </summary>
    [HttpGet("{employeeId:int}/default-hours")]
    public async Task<ActionResult<IReadOnlyList<EmployeeCapacityDailyHoursDto>>> GetDefaultHours(
        int employeeId,
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        CancellationToken cancellationToken)
    {
        if (to < from)
        {
            return BadRequest(new { message = "'to' must be greater than or equal to 'from'." });
        }

        var hours = await _scheduleQueryService.GetDefaultDailyHoursAsync(employeeId, from, to, cancellationToken);
        return Ok(ToDtoList(hours));
    }

    /// <summary>
    /// The employee's effective hours per day: profile hours, overridden per day wherever an
    /// active EmployeeCapacityOverride covers that date. For planning/allocation.
    /// </summary>
    [HttpGet("{employeeId:int}/effective-hours")]
    public async Task<ActionResult<IReadOnlyList<EmployeeCapacityDailyHoursDto>>> GetEffectiveHours(
        int employeeId,
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        CancellationToken cancellationToken)
    {
        if (to < from)
        {
            return BadRequest(new { message = "'to' must be greater than or equal to 'from'." });
        }

        var hours = await _scheduleQueryService.GetEffectiveDailyHoursAsync(employeeId, from, to, cancellationToken);
        return Ok(ToDtoList(hours));
    }

    private static List<EmployeeCapacityDailyHoursDto> ToDtoList(IReadOnlyDictionary<DateOnly, decimal> hours) =>
        hours
            .OrderBy(kvp => kvp.Key)
            .Select(kvp => new EmployeeCapacityDailyHoursDto { Date = kvp.Key, Hours = kvp.Value })
            .ToList();
}
