using System.ComponentModel.DataAnnotations;

namespace Vita.Planning.Application.DTOs;

public sealed class AutoDistributeResourcePlanEntriesRequest
{
    [Required]
    [MinLength(1)]
    public IReadOnlyList<AutoDistributeResourcePlanEntryItemRequest> Distributions { get; set; } = Array.Empty<AutoDistributeResourcePlanEntryItemRequest>();
}

public sealed class AutoDistributeResourcePlanEntryItemRequest : IValidatableObject
{
    [Required]
    public int PlanningTargetId { get; set; }

    [Required]
    public int ResourcePlanId { get; set; }

    [Required]
    public DateOnly FromDate { get; set; }

    [Required]
    public DateOnly ToDate { get; set; }

    [Required]
    public decimal Hours { get; set; }

    [MaxLength(510)]
    public string? Description { get; set; }

    public bool IsManualOverride { get; set; }

    [MaxLength(200)]
    public string? ChangedBy { get; set; }

    /// <summary>Must be one of the activities assigned to PlanningTargetId's project (ext.project_activities), or null.</summary>
    public int? ProjectActivityId { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (ToDate < FromDate)
        {
            yield return new ValidationResult(
                "ToDate must be greater than or equal to FromDate.",
                [nameof(ToDate), nameof(FromDate)]);
        }
    }
}
