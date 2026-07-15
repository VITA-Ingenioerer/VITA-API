using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Vita.Planning.Infrastructure.Data.Entities;

[Table("resource_plan_snapshot_entries", Schema = "core")]
public sealed class ResourcePlanSnapshotEntry
{
    [Key]
    [Column("resource_plan_snapshot_entry_id")]
    public long ResourcePlanSnapshotEntryId { get; set; }

    [Column("resource_plan_snapshot_id")]
    public long ResourcePlanSnapshotId { get; set; }

    [Column("resource_plan_id")]
    public int ResourcePlanId { get; set; }

    [Column("resource_plan_entry_id")]
    public int? ResourcePlanEntryId { get; set; }

    [Column("employee_id")]
    public int EmployeeId { get; set; }

    [Column("scenario_id")]
    public int ScenarioId { get; set; }

    [Column("planning_target_id")]
    public int? PlanningTargetId { get; set; }

    [Column("project_number")]
    public int? ProjectNumber { get; set; }

    [MaxLength(30)]
    [Column("planning_code")]
    public string? PlanningCode { get; set; }

    [MaxLength(150)]
    [Column("display_text")]
    public string? DisplayText { get; set; }

    [Column("year_number")]
    public int YearNumber { get; set; }

    [Column("month_number")]
    public int? MonthNumber { get; set; }

    [Column("week_number")]
    public int? WeekNumber { get; set; }

    [MaxLength(20)]
    [Column("period_type")]
    public string PeriodType { get; set; } = string.Empty;

    [Column("hours", TypeName = "decimal(18,2)")]
    public decimal Hours { get; set; }

    [MaxLength(255)]
    [Column("description")]
    public string? Description { get; set; }

    [Column("is_manual_override")]
    public bool IsManualOverride { get; set; }

    [Column("snapshot_as_of_utc")]
    public DateTime SnapshotAsOfUtc { get; set; }
}
