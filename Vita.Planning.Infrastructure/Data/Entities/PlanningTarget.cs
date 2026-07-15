using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Vita.Planning.Infrastructure.Data.Entities;

[Table("planning_targets", Schema = "core")]
public sealed class PlanningTarget
{
    [Key]
    [Column("planning_target_id")]
    public int PlanningTargetId { get; set; }

    [Required]
    [MaxLength(100)]
    [Column("code")]
    public string Code { get; set; } = string.Empty;

    [Required]
    [MaxLength(510)]
    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(40)]
    [Column("target_type")]
    public string TargetType { get; set; } = string.Empty;

    [Column("ext_project_number")]
    public int? ExtProjectNumber { get; set; }

    [Column("offer_id")]
    public int? OfferId { get; set; }

    [Column("internal_planning_code_id")]
    public int? InternalPlanningCodeId { get; set; }

    [MaxLength(40)]
    [Column("office_code")]
    public string? OfficeCode { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; }

    [Column("is_plannable")]
    public bool IsPlannable { get; set; }

    [Column("created_at_utc")]
    public DateTime CreatedAtUtc { get; set; }

    [Column("updated_at_utc")]
    public DateTime? UpdatedAtUtc { get; set; }
}