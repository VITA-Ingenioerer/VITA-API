using System.ComponentModel.DataAnnotations;

namespace Vita.Planning.Application.DTOs;

public sealed class CreatePublicHolidayCalendarRequest
{
    [Required]
    [MaxLength(20)]
    public string CountryCode { get; set; } = string.Empty;

    [Required]
    public DateOnly HolidayDate { get; set; }

    [Required]
    [MaxLength(300)]
    public string HolidayName { get; set; } = string.Empty;

    public bool IsPublicHoliday { get; set; }
    public bool IsHalfDay { get; set; }
    public decimal? HoursReduction { get; set; }
}
