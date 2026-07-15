namespace Vita.Planning.Application.Interfaces;

/// <summary>
/// Resolves an employee's actual capacity hours per calendar day.
/// </summary>
public interface ICapacityScheduleQueryService
{
    /// <summary>
    /// Profile pattern only, overrides never applied. Use for time registration —
    /// this always reflects what the employee was hired for.
    /// </summary>
    Task<IReadOnlyDictionary<DateOnly, decimal>> GetDefaultDailyHoursAsync(
        int employeeId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default);

    /// <summary>
    /// Profile pattern, with any active EmployeeCapacityOverride layered in per day.
    /// Use for planning/allocation. Pass null for a virtual-resource-backed plan (no
    /// employee capacity profile exists) to get the baseline weekly-hours schedule.
    /// </summary>
    Task<IReadOnlyDictionary<DateOnly, decimal>> GetEffectiveDailyHoursAsync(
        int? employeeId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default);
}
