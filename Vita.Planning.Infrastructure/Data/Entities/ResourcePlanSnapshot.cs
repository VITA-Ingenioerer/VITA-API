using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Vita.Planning.Infrastructure.Data.Entities;

[Table("resource_plan_snapshots", Schema = "core")]
public sealed class ResourcePlanSnapshot
{
    [Key]
    [Column("resource_plan_snapshot_id")]
    public long ResourcePlanSnapshotId { get; set; }

    [Column("scenario_id")]
    public int ScenarioId { get; set; }

    [MaxLength(200)]
    [Column("snapshot_name")]
    public string? SnapshotName { get; set; }

    [MaxLength(30)]
    [Column("snapshot_type")]
    public string SnapshotType { get; set; } = string.Empty;

    [Column("snapshot_as_of_utc")]
    public DateTime SnapshotAsOfUtc { get; set; }

    [MaxLength(100)]
    [Column("created_by")]
    public string? CreatedBy { get; set; }

    [MaxLength(1000)]
    [Column("notes")]
    public string? Notes { get; set; }
}
