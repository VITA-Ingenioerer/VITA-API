using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Vita.Planning.Infrastructure.Data.Entities;

[Table("employee_capacity_periods", Schema = "core")]
public sealed class EmployeeCapacityPeriod
{
    [Key]
    [Column("employee_capacity_period_id")]
    public int EmployeeCapacityPeriodId { get; set; }

    [Column("employee_id")]
    public int EmployeeId { get; set; }

    [Column("period_start")]
    public DateOnly PeriodStart { get; set; }

    [Column("period_end")]
    public DateOnly PeriodEnd { get; set; }

    [MaxLength(20)]
    [Column("period_type")]
    public string PeriodType { get; set; } = string.Empty;

    [Column("weekly_hours_basis")]
    public decimal? WeeklyHoursBasis { get; set; }

    [Column("public_holiday_days")]
    public decimal? PublicHolidayDays { get; set; }

    [Column("capacity_hours")]
    public decimal? CapacityHours { get; set; }

    [Column("is_generated")]
    public bool IsGenerated { get; set; }

    [MaxLength(60)]
    [Column("generation_source")]
    public string GenerationSource { get; set; } = string.Empty;

    [Column("generated_at_utc")]
    public DateTime GeneratedAtUtc { get; set; }
}
