using System.ComponentModel.DataAnnotations;

namespace Vita.Planning.Application.DTOs;

public sealed class CreateResourcePlanEntryRequest
{
    [Required]
    public DateOnly PlanDate { get; set; }

    [Required]
    public decimal Hours { get; set; }

    [MaxLength(510)]
    public string? Description { get; set; }

    public bool IsManualOverride { get; set; }

    [MaxLength(200)]
    public string? CreatedBy { get; set; }

    [Required]
    public int PlanningTargetId { get; set; }

    [Required]
    public int ResourcePlanId { get; set; }

    /// <summary>Must be one of the activities assigned to PlanningTargetId's project (ext.project_activities), or null.</summary>
    public int? ProjectActivityId { get; set; }
}