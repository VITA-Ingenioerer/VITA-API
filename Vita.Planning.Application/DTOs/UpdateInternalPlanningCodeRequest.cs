using System.ComponentModel.DataAnnotations;

namespace Vita.Planning.Application.DTOs;

public sealed class UpdateInternalPlanningCodeRequest
{
    [Required]
    [MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(30)]
    public string Category { get; set; } = string.Empty;

    [MaxLength(20)]
    public string? OfficeCode { get; set; }

    [MaxLength(255)]
    public string? DefaultDescription { get; set; }

    [MaxLength(20)]
    public string? ColorTag { get; set; }

    public bool IsActive { get; set; }
    public bool IsPlannable { get; set; }
    public bool IsAbsence { get; set; }
    public bool IsInternal { get; set; }
    public bool IsBillable { get; set; }

    [MaxLength(100)]
    public string? UpdatedBy { get; set; }
}