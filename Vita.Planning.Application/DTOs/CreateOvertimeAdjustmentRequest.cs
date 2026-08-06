using System.ComponentModel.DataAnnotations;

namespace Vita.Planning.Application.DTOs;

public sealed class CreateOvertimeAdjustmentRequest
{
    [Required]
    public int EmployeeId { get; set; }

    [Required]
    public DateOnly EffectiveMonth { get; set; }

    // A checkpoint, not a delta: the correct flex balance AS OF EffectiveMonth. The refresh
    // job resets running_balance to this value on that date and accumulates normally after.
    [Required]
    public decimal Hours { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }

    public int? CreatedByEmployeeId { get; set; }
}
