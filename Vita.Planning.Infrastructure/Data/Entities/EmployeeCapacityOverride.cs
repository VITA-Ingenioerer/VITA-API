using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Vita.Planning.Infrastructure.Data.Entities;

[Table("employee_capacity_overrides", Schema = "core")]
public sealed class EmployeeCapacityOverride
{
    [Key]
    [Column("employee_capacity_override_id")]
    public int EmployeeCapacityOverrideId { get; set; }

    [Column("employee_id")]
    public int EmployeeId { get; set; }

    [Column("effective_from")]
    public DateOnly EffectiveFrom { get; set; }

    [Column("effective_to")]
    public DateOnly? EffectiveTo { get; set; }

    [MaxLength(60)]
    [Column("override_type")]
    public string OverrideType { get; set; } = string.Empty;

    [Column("weekly_hours")]
    public decimal? WeeklyHours { get; set; }

    [Column("capacity_factor")]
    public decimal? CapacityFactor { get; set; }

    [Column("fixed_hours_per_week")]
    public decimal? FixedHoursPerWeek { get; set; }

    [MaxLength(500)]
    [Column("reason")]
    public string? Reason { get; set; }

    [MaxLength(2000)]
    [Column("notes")]
    public string? Notes { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; }

    [Column("monday_hours")]
    public decimal? MondayHours { get; set; }

    [Column("tuesday_hours")]
    public decimal? TuesdayHours { get; set; }

    [Column("wednesday_hours")]
    public decimal? WednesdayHours { get; set; }

    [Column("thursday_hours")]
    public decimal? ThursdayHours { get; set; }

    [Column("friday_hours")]
    public decimal? FridayHours { get; set; }

    [Column("saturday_hours")]
    public decimal? SaturdayHours { get; set; }

    [Column("sunday_hours")]
    public decimal? SundayHours { get; set; }

    [MaxLength(200)]
    [Column("created_by")]
    public string? CreatedBy { get; set; }

    [MaxLength(200)]
    [Column("updated_by")]
    public string? UpdatedBy { get; set; }

    [Column("created_at_utc")]
    public DateTime CreatedAtUtc { get; set; }

    [Column("updated_at_utc")]
    public DateTime? UpdatedAtUtc { get; set; }
}
