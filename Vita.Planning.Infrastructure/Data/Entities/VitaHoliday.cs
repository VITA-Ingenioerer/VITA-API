using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Vita.Planning.Infrastructure.Data.Entities;

[Keyless]
[Table("vita_holidays", Schema = "core")]
public sealed class VitaHoliday
{
    [Column("vita_holiday_override_id")]
    public int? VitaHolidayOverrideId { get; set; }

    [Column("public_holiday_calendar_id")]
    public int? PublicHolidayCalendarId { get; set; }

    [Column("country_code")]
    public string CountryCode { get; set; } = string.Empty;

    [Column("holiday_date")]
    public DateOnly HolidayDate { get; set; }

    [Column("holiday_name")]
    public string? HolidayName { get; set; }

    [Column("is_active")]
    public bool? IsActive { get; set; }

    [Column("is_half_day")]
    public bool? IsHalfDay { get; set; }

    [Column("hours_reduction")]
    public decimal? HoursReduction { get; set; }

    [Column("holiday_type")]
    public string? HolidayType { get; set; }

    [Column("notes")]
    public string? Notes { get; set; }

    [Column("created_at_utc")]
    public DateTime CreatedAtUtc { get; set; }

    [Column("updated_at_utc")]
    public DateTime? UpdatedAtUtc { get; set; }
}
