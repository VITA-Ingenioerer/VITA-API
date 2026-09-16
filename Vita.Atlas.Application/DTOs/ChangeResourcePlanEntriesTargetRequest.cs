using System.ComponentModel.DataAnnotations;

namespace Vita.Atlas.Application.DTOs;

/// <summary>
/// Moves one employee's whole plan line from one project to another within the same
/// project family — main project to subproject, or subproject to subproject.
///
/// Like <see cref="ChangeResourcePlanEntriesActivityRequest"/> it takes no date range:
/// the line moves whole, so nothing is left behind outside the caller's loaded window.
/// </summary>
public sealed class ChangeResourcePlanEntriesTargetRequest : IValidatableObject
{
    [Required]
    public int ResourcePlanId { get; set; }

    [Required]
    public int FromPlanningTargetId { get; set; }

    [Required]
    public int ToPlanningTargetId { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (FromPlanningTargetId == ToPlanningTargetId)
        {
            yield return new ValidationResult(
                "FromPlanningTargetId and ToPlanningTargetId must differ.",
                [nameof(FromPlanningTargetId), nameof(ToPlanningTargetId)]);
        }
    }
}
