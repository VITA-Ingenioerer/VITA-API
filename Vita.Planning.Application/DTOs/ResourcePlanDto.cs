namespace Vita.Planning.Application.DTOs;

public sealed class ResourcePlanDto
{
    public int ResourcePlanId { get; set; }
    public int? EmployeeId { get; set; }
    public int? VirtualResourceId { get; set; }
    public int ScenarioId { get; set; }
    public int StartYear { get; set; }
    public int StartMonth { get; set; }
    public int VisibleMonths { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; }
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}