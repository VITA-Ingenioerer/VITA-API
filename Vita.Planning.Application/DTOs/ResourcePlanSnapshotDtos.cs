namespace Vita.Planning.Application.DTOs;

public sealed class CreateResourcePlanSnapshotRequest
{
    public int ScenarioId { get; set; }
    public string? SnapshotName { get; set; }
    public string SnapshotType { get; set; } = "Manual";
    public string PeriodType { get; set; } = "Month";
    public DateOnly? FromDate { get; set; }
    public DateOnly? ToDate { get; set; }
    public string? Notes { get; set; }
}

public sealed class ResourcePlanSnapshotDto
{
    public long ResourcePlanSnapshotId { get; set; }
    public int ScenarioId { get; set; }
    public string? SnapshotName { get; set; }
    public string SnapshotType { get; set; } = string.Empty;
    public DateTime SnapshotAsOfUtc { get; set; }
    public string? CreatedBy { get; set; }
    public string? Notes { get; set; }
    public int EntryCount { get; set; }
    public decimal TotalHours { get; set; }
}

public sealed class ResourcePlanSnapshotEntryDto
{
    public long ResourcePlanSnapshotEntryId { get; set; }
    public long ResourcePlanSnapshotId { get; set; }
    public int ResourcePlanId { get; set; }
    public int? ResourcePlanEntryId { get; set; }
    public int EmployeeId { get; set; }
    public int ScenarioId { get; set; }
    public int? PlanningTargetId { get; set; }
    public int? ProjectNumber { get; set; }
    public string? PlanningCode { get; set; }
    public string? DisplayText { get; set; }
    public int YearNumber { get; set; }
    public int? MonthNumber { get; set; }
    public int? WeekNumber { get; set; }
    public string PeriodType { get; set; } = string.Empty;
    public decimal Hours { get; set; }
    public string? Description { get; set; }
    public bool IsManualOverride { get; set; }
    public DateTime SnapshotAsOfUtc { get; set; }
}

public sealed class ResourcePlanSnapshotComparisonDto
{
    public long FromSnapshotId { get; set; }
    public long ToSnapshotId { get; set; }
    public IReadOnlyList<ResourcePlanSnapshotChangeDto> Changes { get; set; } = [];
}

public sealed class ResourcePlanSnapshotChangeDto
{
    public int ResourcePlanId { get; set; }
    public int EmployeeId { get; set; }
    public int? PlanningTargetId { get; set; }
    public int? ProjectNumber { get; set; }
    public string? PlanningCode { get; set; }
    public string? DisplayText { get; set; }
    public int YearNumber { get; set; }
    public int? MonthNumber { get; set; }
    public int? WeekNumber { get; set; }
    public string PeriodType { get; set; } = string.Empty;
    public decimal OldHours { get; set; }
    public decimal NewHours { get; set; }
    public decimal DeltaHours { get; set; }
    public string ChangeType { get; set; } = string.Empty;
}
