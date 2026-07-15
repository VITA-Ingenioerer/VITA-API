namespace Vita.Planning.Application.DTOs;

public sealed class PublicHolidayCalendarDto
{
    public int PublicHolidayCalendarId { get; set; }
    public string CountryCode { get; set; } = string.Empty;
    public DateOnly HolidayDate { get; set; }
    public string HolidayName { get; set; } = string.Empty;
    public bool IsPublicHoliday { get; set; }
    public bool IsHalfDay { get; set; }
    public decimal? HoursReduction { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
