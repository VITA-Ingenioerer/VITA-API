using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Vita.Planning.Infrastructure.Data.Entities;

[Table("internal_planning_codes", Schema = "core")]
public sealed class InternalPlanningCode
{
    [Key]
    [Column("internal_planning_code_id")]
    public int InternalPlanningCodeId { get; set; }

    [Required]
    [MaxLength(50)]
    [Column("code")]
    public string Code { get; set; } = string.Empty;

    [Required]
    [MaxLength(150)]
    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(30)]
    [Column("category")]
    public string Category { get; set; } = string.Empty;

    [MaxLength(20)]
    [Column("office_code")]
    public string? OfficeCode { get; set; }

    [MaxLength(255)]
    [Column("default_description")]
    public string? DefaultDescription { get; set; }

    [MaxLength(20)]
    [Column("color_tag")]
    public string? ColorTag { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; }

    [Column("is_plannable")]
    public bool IsPlannable { get; set; }

    [Column("is_absence")]
    public bool IsAbsence { get; set; }

    [Column("is_internal")]
    public bool IsInternal { get; set; }

    [Column("is_billable")]
    public bool IsBillable { get; set; }

    [MaxLength(100)]
    [Column("created_by")]
    public string? CreatedBy { get; set; }

    [MaxLength(100)]
    [Column("updated_by")]
    public string? UpdatedBy { get; set; }

    [Column("created_at_utc")]
    public DateTime CreatedAtUtc { get; set; }

    [Column("updated_at_utc")]
    public DateTime? UpdatedAtUtc { get; set; }
}