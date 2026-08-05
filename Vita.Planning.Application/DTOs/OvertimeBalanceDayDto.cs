namespace Vita.Planning.Application.DTOs;

public sealed class OvertimeBalanceDayDto
{
    public int EmployeeId { get; set; }
    public string? DisplayName { get; set; }
    public DateOnly WorkDate { get; set; }
    public decimal ActualHours { get; set; }
    public decimal ExpectedHours { get; set; }
    public decimal AdjustmentHours { get; set; }
    public decimal DailyDelta { get; set; }
    public decimal RunningBalance { get; set; }
}
