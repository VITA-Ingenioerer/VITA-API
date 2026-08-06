using Vita.Planning.Application.DTOs;

namespace Vita.Planning.Application.Interfaces;

public interface IOvertimeBalanceRefreshService
{
    /// <summary>
    /// Recomputes core.overtime_balance_daily only for employees with time entries or
    /// adjustments newer than the last refresh watermark. Safe to run on a schedule.
    /// </summary>
    Task<OvertimeBalanceRefreshResultDto> RefreshChangedEmployeesAsync(
        string initiatedBy, CancellationToken cancellationToken = default);

    /// <summary>
    /// Recomputes core.overtime_balance_daily for every employee. Needed for the initial
    /// backfill, and after changes that the incremental watermark can't detect (e.g. edits
    /// to capacity profiles, overrides, or holiday calendars).
    /// </summary>
    Task<OvertimeBalanceRefreshResultDto> RefreshAllAsync(
        string initiatedBy, CancellationToken cancellationToken = default);
}
