namespace Vita.Planning.Application.DTOs;

public sealed class RecordEntityChangeRequest
{
    public string EventType { get; set; } = string.Empty;
    public string? EventTitle { get; set; }
    public string? EventDescription { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public int? PlanningTargetId { get; set; }
    public object? OldSnapshot { get; set; }
    public object? NewSnapshot { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public string? ChangeReason { get; set; }
    public CallerInfo Caller { get; set; } = CallerInfo.System;
    public string? SourceModule { get; set; }
    public Guid? CorrelationId { get; set; }
}
