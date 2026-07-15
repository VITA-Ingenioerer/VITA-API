using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Vita.Planning.Infrastructure.Data.Entities;

[Table("project_lifecycle_log", Schema = "core")]
public sealed class ProjectLifecycleLog
{
    [Key]
    [Column("project_lifecycle_log_id")]
    public long ProjectLifecycleLogId { get; set; }

    [Required]
    [MaxLength(20)]
    [Column("target_type")]
    public string TargetType { get; set; } = string.Empty;

    [Column("project_number")]
    public int? ProjectNumber { get; set; }

    [Column("offer_id")]
    public int? OfferId { get; set; }

    [Required]
    [MaxLength(50)]
    [Column("event_type")]
    public string EventType { get; set; } = string.Empty;

    [MaxLength(255)]
    [Column("event_title")]
    public string? EventTitle { get; set; }

    [Column("event_description")]
    public string? EventDescription { get; set; }

    [Column("old_value")]
    public string? OldValue { get; set; }

    [Column("new_value")]
    public string? NewValue { get; set; }

    [Column("snapshot_json")]
    public string? SnapshotJson { get; set; }

    [MaxLength(100)]
    [Column("created_by")]
    public string? CreatedBy { get; set; }

    [Column("created_at_utc")]
    public DateTime CreatedAtUtc { get; set; }

    [Column("correlation_id")]
    public Guid? CorrelationId { get; set; }
}
