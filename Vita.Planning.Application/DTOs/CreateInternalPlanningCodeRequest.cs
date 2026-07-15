using System.ComponentModel.DataAnnotations;

namespace Vita.Planning.Application.DTOs;

public sealed class CreateInternalPlanningCodeRequest
{
    [Required]
    [MaxLength(50)]
    public string Code { get; set; } = string.Empty;

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

    public bool IsActive { get; set; } = true;
    public bool IsPlannable { get; set; } = true;
    public bool IsAbsence { get; set; } = false;
    public bool IsInternal { get; set; } = true;
    public bool IsBillable { get; set; } = false;

    [MaxLength(100)]
    public string? CreatedBy { get; set; }
}