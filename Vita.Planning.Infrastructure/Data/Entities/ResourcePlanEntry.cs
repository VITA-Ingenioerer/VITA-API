using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Vita.Planning.Infrastructure.Data.Entities;

[Table("resource_plan_entries", Schema = "core")]
public sealed class ResourcePlanEntry
{
    [Key]
    [Column("resource_plan_entry_id")]
    public int ResourcePlanEntryId { get; set; }

    [Column("resource_plan_id")]
    public int ResourcePlanId { get; set; }

    [Column("hours", TypeName = "decimal(18,2)")]
    public decimal Hours { get; set; }

    [MaxLength(510)]
    [Column("description")]
    public string? Description { get; set; }

    [Column("is_manual_override")]
    public bool IsManualOverride { get; set; }

    [MaxLength(200)]
    [Column("created_by")]
    public string? CreatedBy { get; set; }

    [MaxLength(200)]
    [Column("updated_by")]
    public string? UpdatedBy { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    [Column("planning_target_id")]
    public int PlanningTargetId { get; set; }

    [Column("plan_date")]
    public DateOnly PlanDate { get; set; }

    [Column("ext_project_activity_number")]
    public int? ProjectActivityId { get; set; }

    public PlanningTarget? PlanningTarget { get; set; }
    public ExtProjectActivity? ProjectActivity { get; set; }
    public ResourcePlan? ResourcePlan { get; set; }
}