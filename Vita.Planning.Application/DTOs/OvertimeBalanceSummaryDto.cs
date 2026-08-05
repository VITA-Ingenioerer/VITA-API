namespace Vita.Planning.Application.DTOs;

public sealed class OvertimeBalanceSummaryDto
{
    public int EmployeeId { get; set; }
    public string? DisplayName { get; set; }
    public DateOnly AsOfDate { get; set; }
    public decimal RunningBalance { get; set; }
}
