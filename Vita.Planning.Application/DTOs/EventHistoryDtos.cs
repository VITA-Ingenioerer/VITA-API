namespace Vita.Planning.Application.DTOs;

public sealed class ResourcePlanEntryHistoryDto
{
    public long ResourcePlanEntryHistoryId { get; set; }
    public int? ResourcePlanEntryId { get; set; }
    public int ResourcePlanId { get; set; }
    public int EmployeeId { get; set; }
    public int ScenarioId { get; set; }
    public int PlanningTargetId { get; set; }
    public string? PlanningTargetName { get; set; }
    public DateOnly PlanDate { get; set; }
    public decimal? OldHours { get; set; }
    public decimal? NewHours { get; set; }
    public string? OldDescription { get; set; }
    public string? NewDescription { get; set; }
    public bool? OldIsManualOverride { get; set; }
    public bool? NewIsManualOverride { get; set; }
    public string ChangeType { get; set; } = string.Empty;
    public string? ChangeReason { get; set; }
    public string? ChangedByUserId { get; set; }
    public string? ChangedByName { get; set; }
    public DateTime ChangedAtUtc { get; set; }
    public string? SourceModule { get; set; }
    public Guid CorrelationId { get; set; }
    public string? MetadataJson { get; set; }
}

public sealed class BusinessEventDto
{
    public long BusinessEventId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string? EventTitle { get; set; }
    public string? EventDescription { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public int? PlanningTargetId { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public string? PayloadJson { get; set; }
    public string? CreatedByUserId { get; set; }
    public string? CreatedByName { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public string? SourceModule { get; set; }
    public Guid CorrelationId { get; set; }
}

public sealed class RecordResourcePlanEntryHistoryRequest
{
    public int? ResourcePlanEntryId { get; set; }
    public int ResourcePlanId { get; set; }
    public int EmployeeId { get; set; }
    public int ScenarioId { get; set; }
    public int PlanningTargetId { get; set; }
    public DateOnly PlanDate { get; set; }
    public decimal? OldHours { get; set; }
    public decimal? NewHours { get; set; }
    public string? OldDescription { get; set; }
    public string? NewDescription { get; set; }
    public bool? OldIsManualOverride { get; set; }
    public bool? NewIsManualOverride { get; set; }
    public string ChangeType { get; set; } = string.Empty;
    public string? ChangeReason { get; set; }
    public string? ChangedByUserId { get; set; }
    public string? ChangedByName { get; set; }
    public string? SourceModule { get; set; }
    public Guid? CorrelationId { get; set; }
    public string? MetadataJson { get; set; }
}

public sealed class RecordBusinessEventRequest
{
    public string EventType { get; set; } = string.Empty;
    public string? EventTitle { get; set; }
    public string? EventDescription { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public int? PlanningTargetId { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public string? PayloadJson { get; set; }
    public string? CreatedByUserId { get; set; }
    public string? CreatedByName { get; set; }
    public string? SourceModule { get; set; }
    public Guid? CorrelationId { get; set; }
}
