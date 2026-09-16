using System.ComponentModel.DataAnnotations;

namespace Vita.Atlas.Application.DTOs;

public sealed class CreatePlanningTargetRequest
{
    [Required]
    [MaxLength(100)]
    public string Code { get; set; } = string.Empty;

    [Required]
    [MaxLength(510)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(40)]
    public string TargetType { get; set; } = string.Empty;

    public int? ExtProjectNumber { get; set; }
    public int? OfferId { get; set; }
    public int? InternalPlanningCodeId { get; set; }

    [MaxLength(40)]
    public string? OfficeCode { get; set; }

    public bool IsActive { get; set; }
    // Every other planning-target creation site already hardcodes true; this was the
    // one path where omitting the flag produced a target the planner would never show.
    public bool IsPlannable { get; set; } = true;
}