using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Vita.Planning.Infrastructure.Data.Entities;

[Table("resource_plan_scenarios", Schema = "core")]
public sealed class ResourcePlanScenario
{
    [Key]
    [Column("scenario_id")]
    public int ScenarioId { get; set; }

    [MaxLength(200)]
    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [MaxLength(510)]
    [Column("description")]
    public string? Description { get; set; }

    [MaxLength(200)]
    [Column("created_by")]
    public string? CreatedBy { get; set; }

    [MaxLength(200)]
    [Column("updated_by")]
    public string? UpdatedBy { get; set; }

    [Column("is_default")]
    public bool IsDefault { get; set; }

    [Column("is_locked")]
    public bool IsLocked { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }
}
