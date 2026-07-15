using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Vita.Planning.Infrastructure.Data.Entities;

[Table("public_holiday_calendar", Schema = "core")]
public sealed class PublicHolidayCalendar
{
    [Key]
    [Column("public_holiday_calendar_id")]
    public int PublicHolidayCalendarId { get; set; }

    [MaxLength(20)]
    [Column("country_code")]
    public string CountryCode { get; set; } = string.Empty;

    [Column("holiday_date")]
    public DateOnly HolidayDate { get; set; }

    [MaxLength(300)]
    [Column("holiday_name")]
    public string HolidayName { get; set; } = string.Empty;

    [Column("is_public_holiday")]
    public bool IsPublicHoliday { get; set; }

    [Column("is_half_day")]
    public bool IsHalfDay { get; set; }

    [Column("hours_reduction")]
    public decimal? HoursReduction { get; set; }

    [Column("created_at_utc")]
    public DateTime CreatedAtUtc { get; set; }
}
