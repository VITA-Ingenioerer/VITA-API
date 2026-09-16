namespace Vita.Atlas.Application.DTOs;

public sealed class ProjectListItemDto
{
    public int ProjectNumber { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public bool IsMainProject { get; set; }
    public int? MainProjectNumber { get; set; }
    public int? ResponsibleEmployeeNumber { get; set; }
    public int? StatusNumber { get; set; }
    /// <summary>e-conomic's own status name for the project, resolved from ext.project_statuses.</summary>
    public string? StatusName { get; set; }
    public bool IsClosed { get; set; }
    public bool IsBarred { get; set; }
    public DateTime? DeliveryDate { get; set; }
    public string? PlanningStatus { get; set; }
    public bool IsVisibleInPlanner { get; set; }
    /// <summary>SharePoint project archive, when one has been provisioned.</summary>
    public string? ProjectArchiveUrl { get; set; }
    /// <summary>
    /// When hours on this project were last planned or changed — the newest create/update
    /// stamp across its resource plan entries. Null means nobody has planned on it.
    /// </summary>
    public DateTime? LastResourcePlanActivityUtc { get; set; }
}