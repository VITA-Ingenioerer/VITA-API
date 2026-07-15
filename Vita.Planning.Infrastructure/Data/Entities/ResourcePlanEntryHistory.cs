using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Vita.Planning.Infrastructure.Data.Entities;

[Table("resource_plan_entry_history", Schema = "core")]
public sealed class ResourcePlanEntryHistory
{
    [Key]
    [Column("resource_plan_entry_history_id")]
    public long ResourcePlanEntryHistoryId { get; set; }

    [Column("resource_plan_entry_id")]
    public int? ResourcePlanEntryId { get; set; }

    [Column("resource_plan_id")]
    public int ResourcePlanId { get; set; }

    [Column("employee_id")]
    public int EmployeeId { get; set; }

    [Column("scenario_id")]
    public int ScenarioId { get; set; }

    [Column("planning_target_id")]
    public int PlanningTargetId { get; set; }

    [Column("plan_date")]
    public DateOnly PlanDate { get; set; }

    [Column("old_hours", TypeName = "decimal(18,2)")]
    public decimal? OldHours { get; set; }

    [Column("new_hours", TypeName = "decimal(18,2)")]
    public decimal? NewHours { get; set; }

    [MaxLength(255)]
    [Column("old_description")]
    public string? OldDescription { get; set; }

    [MaxLength(255)]
    [Column("new_description")]
    public string? NewDescription { get; set; }

    [Column("old_is_manual_override")]
    public bool? OldIsManualOverride { get; set; }

    [Column("new_is_manual_override")]
    public bool? NewIsManualOverride { get; set; }

    [Required]
    [MaxLength(30)]
    [Column("change_type")]
    public string ChangeType { get; set; } = string.Empty;

    [MaxLength(500)]
    [Column("change_reason")]
    public string? ChangeReason { get; set; }

    [MaxLength(320)]
    [Column("changed_by_user_id")]
    public string? ChangedByUserId { get; set; }

    [MaxLength(200)]
    [Column("changed_by_name")]
    public string? ChangedByName { get; set; }

    [Column("changed_at_utc")]
    public DateTime ChangedAtUtc { get; set; }

    [MaxLength(100)]
    [Column("source_module")]
    public string? SourceModule { get; set; }

    [Column("correlation_id")]
    public Guid CorrelationId { get; set; } = Guid.NewGuid();

    [Column("metadata_json")]
    public string? MetadataJson { get; set; }

    public ResourcePlanEntry? ResourcePlanEntry { get; set; }
    public PlanningTarget? PlanningTarget { get; set; }
}
