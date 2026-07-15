namespace Vita.Planning.Application.DTOs;

public sealed class ResourcePlanEntryDto
{
    public int ResourcePlanEntryId { get; set; }
    public int ResourcePlanId { get; set; }
    public int? EmployeeId { get; set; }
    public int? VirtualResourceId { get; set; }
    public int ScenarioId { get; set; }
    public decimal Hours { get; set; }
    public string? Description { get; set; }
    public bool IsManualOverride { get; set; }
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public int PlanningTargetId { get; set; }
    public string? PlanningTargetCode { get; set; }
    public string? PlanningTargetName { get; set; }
    public string? PlanningTargetType { get; set; }
    public int? ProjectNumber { get; set; }
    public int? OfferId { get; set; }
    public int? InternalPlanningCodeId { get; set; }
    public DateOnly PlanDate { get; set; }
    public int? ProjectActivityId { get; set; }
    public int? ActivityNumber { get; set; }
    public string? ActivityName { get; set; }
}