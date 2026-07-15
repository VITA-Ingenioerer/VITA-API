namespace Vita.Planning.Application.DTOs;

public sealed class VitaHolidayOverrideDto
{
    public int VitaHolidayOverrideId { get; set; }
    public string CountryCode { get; set; } = string.Empty;
    public DateOnly HolidayDate { get; set; }
    public string? HolidayName { get; set; }
    public bool IsActive { get; set; }
    public bool? IsHalfDay { get; set; }
    public decimal? HoursReduction { get; set; }
    public string HolidayType { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
}
