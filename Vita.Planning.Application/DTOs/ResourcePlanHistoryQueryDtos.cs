namespace Vita.Planning.Application.DTOs;

/// <summary>
/// One logical change — all resource_plan_entry_history rows sharing a correlation id,
/// collapsed into a single logbook line (e.g. one bulk save or one auto-distribute call).
/// </summary>
public sealed class ResourcePlanLogbookEntryDto
{
    public Guid CorrelationId { get; set; }
    public DateTime ChangedAtUtc { get; set; }
    public string? ChangedByUserId { get; set; }
    public string? ChangedByName { get; set; }
    public string? SourceModule { get; set; }
    public string? ChangeReason { get; set; }
    public string ChangeType { get; set; } = string.Empty;
    public int EntryCount { get; set; }
    public decimal TotalHoursDelta { get; set; }
    public IReadOnlyList<int> EmployeeIds { get; set; } = [];
    public IReadOnlyList<int> PlanningTargetIds { get; set; } = [];
    public DateOnly PlanDateFrom { get; set; }
    public DateOnly PlanDateTo { get; set; }
}

/// <summary>
/// A single resource plan entry's hours reconstructed as of a given point in time,
/// derived from resource_plan_entry_history (no separate snapshot required).
/// </summary>
public sealed class ResourcePlanAsOfEntryDto
{
    public int EmployeeId { get; set; }
    public int ScenarioId { get; set; }
    public int PlanningTargetId { get; set; }
    public string? PlanningTargetCode { get; set; }
    public string? PlanningTargetName { get; set; }
    public int? ProjectNumber { get; set; }
    public DateOnly PlanDate { get; set; }
    public decimal Hours { get; set; }
    public string? Description { get; set; }
    public bool IsManualOverride { get; set; }
}

/// <summary>
/// The delta for one (employee, planning target, day) between two "as of" reconstructions.
/// </summary>
public sealed class ResourcePlanAsOfComparisonEntryDto
{
    public int EmployeeId { get; set; }
    public int PlanningTargetId { get; set; }
    public string? PlanningTargetCode { get; set; }
    public string? PlanningTargetName { get; set; }
    public int? ProjectNumber { get; set; }
    public DateOnly PlanDate { get; set; }
    public decimal OldHours { get; set; }
    public decimal NewHours { get; set; }
    public decimal DeltaHours { get; set; }
}
