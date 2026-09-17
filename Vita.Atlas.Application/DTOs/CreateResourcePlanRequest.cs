using System.ComponentModel.DataAnnotations;

namespace Vita.Atlas.Application.DTOs;

public sealed class CreateResourcePlanRequest
{
    /// <summary>
    /// The employee this plan belongs to. Mutually exclusive with
    /// <see cref="VirtualResourceId"/> — a plan belongs to exactly one of them, which is what
    /// core.resource_plans models with its two nullable columns.
    /// </summary>
    public int? EmployeeId { get; set; }

    /// <summary>
    /// An unfilled role (NN-BIM) or a named external (Lars J at PLH arkitekter), instead of an
    /// employee. Hours are written against the resulting ResourcePlanId exactly as for a person.
    /// </summary>
    public int? VirtualResourceId { get; set; }

    [Required]
    public int ScenarioId { get; set; }

    [Required]
    public int StartYear { get; set; }

    [Required]
    public int StartMonth { get; set; }

    [Required]
    public int VisibleMonths { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    public bool IsActive { get; set; }

    [MaxLength(200)]
    public string? CreatedBy { get; set; }
}