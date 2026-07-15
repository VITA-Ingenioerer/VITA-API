using System.ComponentModel.DataAnnotations;

namespace Vita.Planning.Application.DTOs;

public sealed class CreateVitaHolidayOverrideRequest
{
    [Required]
    [MaxLength(20)]
    public string CountryCode { get; set; } = string.Empty;

    [Required]
    public DateOnly HolidayDate { get; set; }

    [MaxLength(300)]
    public string? HolidayName { get; set; }

    public bool IsActive { get; set; } = true;
    public bool? IsHalfDay { get; set; }
    public decimal? HoursReduction { get; set; }

    [Required]
    [MaxLength(60)]
    public string HolidayType { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Notes { get; set; }
}
