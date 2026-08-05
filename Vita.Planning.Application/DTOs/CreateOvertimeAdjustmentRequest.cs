using System.ComponentModel.DataAnnotations;

namespace Vita.Planning.Application.DTOs;

public sealed class CreateOvertimeAdjustmentRequest
{
    [Required]
    public int EmployeeId { get; set; }

    [Required]
    public DateOnly EffectiveMonth { get; set; }

    [Required]
    public decimal Hours { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }

    public int? CreatedByEmployeeId { get; set; }
}
