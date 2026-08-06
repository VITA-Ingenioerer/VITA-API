using System.ComponentModel.DataAnnotations.Schema;

namespace Vita.Planning.Infrastructure.Data.Entities;

/// <summary>
/// Materialized, per-employee-per-day overtime/flex balance. Refreshed by
/// IOvertimeBalanceRefreshService (incrementally for changed employees on every scheduled
/// sync, or in full on demand) rather than computed live — the underlying calculation
/// (calendar + capacity profile/override + holiday resolution + running total) is too
/// expensive to redo on every read, especially across the whole company.
/// </summary>
[Table("overtime_balance_daily", Schema = "core")]
public sealed class OvertimeBalanceDaily
{
    [Column("employee_id")]
    public int EmployeeId { get; set; }

    [Column("work_date")]
    public DateOnly WorkDate { get; set; }

    [Column("actual_hours")]
    public decimal ActualHours { get; set; }

    [Column("expected_hours")]
    public decimal ExpectedHours { get; set; }

    [Column("adjustment_hours")]
    public decimal AdjustmentHours { get; set; }

    [Column("daily_delta")]
    public decimal DailyDelta { get; set; }

    [Column("running_balance")]
    public decimal RunningBalance { get; set; }

    [Column("computed_at_utc")]
    public DateTime ComputedAtUtc { get; set; }
}
