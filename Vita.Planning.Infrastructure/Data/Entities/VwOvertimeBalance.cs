using System.ComponentModel.DataAnnotations.Schema;

namespace Vita.Planning.Infrastructure.Data.Entities;

/// <summary>
/// Maps to core.vw_overtime_balance — daily actual-vs-expected hours plus manual
/// adjustments, with a running cumulative balance per employee. Read-only; the view
/// is the single source for both the "flex" and "overtime" numbers.
/// </summary>
[Table("vw_overtime_balance", Schema = "core")]
public sealed class VwOvertimeBalance
{
    [Column("employee_id")]
    public int EmployeeId { get; set; }

    [Column("display_name")]
    public string? DisplayName { get; set; }

    [Column("work_date")]
    public DateOnly WorkDate { get; set; }

    [Column("actual_hours")]
    public decimal? ActualHours { get; set; }

    [Column("expected_hours")]
    public decimal? ExpectedHours { get; set; }

    [Column("adjustment_hours")]
    public decimal? AdjustmentHours { get; set; }

    [Column("daily_delta")]
    public decimal? DailyDelta { get; set; }

    [Column("running_balance")]
    public decimal? RunningBalance { get; set; }
}
