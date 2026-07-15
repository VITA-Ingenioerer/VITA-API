namespace Vita.Planning.Application.DTOs;

public sealed class ProjectLifecycleLogDto
{
    public long ProjectLifecycleLogId { get; set; }
    public string TargetType { get; set; } = string.Empty;
    public int? ProjectNumber { get; set; }
    public int? OfferId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string? EventTitle { get; set; }
    public string? EventDescription { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public string? SnapshotJson { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public Guid? CorrelationId { get; set; }
}
