namespace Vita.Planning.Application.DTOs;

public sealed class OvertimeTrendPointDto
{
    public DateOnly WorkDate { get; set; }
    public decimal TotalBalance { get; set; }
    public decimal AverageBalance { get; set; }
    public int EmployeeCount { get; set; }
}
