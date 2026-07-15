using System.ComponentModel.DataAnnotations;

namespace Vita.Planning.Application.DTOs;

public sealed class UpdatePlanningTargetRequest
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
    public bool IsPlannable { get; set; }
}