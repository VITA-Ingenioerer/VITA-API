namespace Vita.Planning.Application.DTOs;

public sealed class ProjectListItemDto
{
    public int ProjectNumber { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public bool IsMainProject { get; set; }
    public int? MainProjectNumber { get; set; }
    public int? ResponsibleEmployeeNumber { get; set; }
    public bool IsClosed { get; set; }
    public bool IsBarred { get; set; }
    public DateTime? DeliveryDate { get; set; }
    public string? PlanningStatus { get; set; }
    public bool IsVisibleInPlanner { get; set; }
}