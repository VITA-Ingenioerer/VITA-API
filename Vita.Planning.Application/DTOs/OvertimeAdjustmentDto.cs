namespace Vita.Planning.Application.DTOs;

public sealed class OvertimeAdjustmentDto
{
    public int OvertimeAdjustmentId { get; set; }
    public int EmployeeId { get; set; }
    public DateOnly EffectiveMonth { get; set; }
    public decimal Hours { get; set; }
    public string? Notes { get; set; }
    public int? CreatedByEmployeeId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
