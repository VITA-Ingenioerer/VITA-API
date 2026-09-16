using System.ComponentModel.DataAnnotations;

namespace Vita.Atlas.Application.DTOs;

/// <summary>
/// Re-points a whole plan line — one employee's entries on one planning target and one
/// activity — onto a different activity in a single operation.
///
/// Deliberately takes no date range: the line moves whole. The planner only ever has a
/// window of periods loaded, so a range would silently leave entries outside that window
/// behind on the old activity, which is the very thing this endpoint exists to stop.
/// </summary>
public sealed class ChangeResourcePlanEntriesActivityRequest : IValidatableObject
{
    [Required]
    public int ResourcePlanId { get; set; }

    [Required]
    public int PlanningTargetId { get; set; }

    /// <summary>Activity the entries sit on today. Null is the "Ikke-tildelt" bucket.</summary>
    public int? FromProjectActivityId { get; set; }

    /// <summary>Activity they should end up on. Null moves them into the "Ikke-tildelt" bucket.</summary>
    public int? ToProjectActivityId { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (FromProjectActivityId == ToProjectActivityId)
        {
            yield return new ValidationResult(
                "FromProjectActivityId and ToProjectActivityId must differ.",
                [nameof(FromProjectActivityId), nameof(ToProjectActivityId)]);
        }
    }
}
