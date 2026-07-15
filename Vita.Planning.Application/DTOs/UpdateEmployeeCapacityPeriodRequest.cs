using System.ComponentModel.DataAnnotations;

namespace Vita.Planning.Application.DTOs;

public sealed class UpdateEmployeeCapacityPeriodRequest
{
    [Required]
    public int EmployeeId { get; set; }

    [Required]
    public DateOnly PeriodStart { get; set; }

    [Required]
    public DateOnly PeriodEnd { get; set; }

    [Required]
    [MaxLength(20)]
    public string PeriodType { get; set; } = string.Empty;

    public decimal? WeeklyHoursBasis { get; set; }
    public decimal? PublicHolidayDays { get; set; }
    public decimal? CapacityHours { get; set; }
    public bool IsGenerated { get; set; }

    [Required]
    [MaxLength(60)]
    public string GenerationSource { get; set; } = string.Empty;
}
