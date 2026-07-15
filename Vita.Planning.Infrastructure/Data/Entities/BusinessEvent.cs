using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Vita.Planning.Infrastructure.Data.Entities;

[Table("business_events", Schema = "core")]
public sealed class BusinessEvent
{
    [Key]
    [Column("business_event_id")]
    public long BusinessEventId { get; set; }

    [Required]
    [MaxLength(100)]
    [Column("event_type")]
    public string EventType { get; set; } = string.Empty;

    [MaxLength(255)]
    [Column("event_title")]
    public string? EventTitle { get; set; }

    [Column("event_description")]
    public string? EventDescription { get; set; }

    [Required]
    [MaxLength(100)]
    [Column("entity_type")]
    public string EntityType { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    [Column("entity_id")]
    public string EntityId { get; set; } = string.Empty;

    [Column("planning_target_id")]
    public int? PlanningTargetId { get; set; }

    [Column("old_value")]
    public string? OldValue { get; set; }

    [Column("new_value")]
    public string? NewValue { get; set; }

    [Column("payload_json")]
    public string? PayloadJson { get; set; }

    [MaxLength(320)]
    [Column("created_by_user_id")]
    public string? CreatedByUserId { get; set; }

    [MaxLength(200)]
    [Column("created_by_name")]
    public string? CreatedByName { get; set; }

    [Column("created_at_utc")]
    public DateTime CreatedAtUtc { get; set; }

    [MaxLength(100)]
    [Column("source_module")]
    public string? SourceModule { get; set; }

    [Column("correlation_id")]
    public Guid CorrelationId { get; set; } = Guid.NewGuid();

    public PlanningTarget? PlanningTarget { get; set; }
}
