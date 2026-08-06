using System.ComponentModel.DataAnnotations.Schema;

namespace Vita.Planning.Infrastructure.Data.Entities;

/// <summary>
/// Shape of one row returned by OvertimeBalanceRefreshService's per-employee computation
/// query. Not mapped to any table/view — exists only to receive FromSqlRaw results.
/// </summary>
public sealed class OvertimeBalanceComputedRow
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
}
