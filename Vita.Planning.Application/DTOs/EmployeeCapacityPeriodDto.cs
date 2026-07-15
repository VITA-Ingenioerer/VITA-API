namespace Vita.Planning.Application.DTOs;

public sealed class EmployeeCapacityPeriodDto
{
    public int EmployeeCapacityPeriodId { get; set; }
    public int EmployeeId { get; set; }
    public DateOnly PeriodStart { get; set; }
    public DateOnly PeriodEnd { get; set; }
    public string PeriodType { get; set; } = string.Empty;
    public decimal? WeeklyHoursBasis { get; set; }
    public decimal? PublicHolidayDays { get; set; }
    public decimal? CapacityHours { get; set; }
    public bool IsGenerated { get; set; }
    public string GenerationSource { get; set; } = string.Empty;
    public DateTime GeneratedAtUtc { get; set; }
}
