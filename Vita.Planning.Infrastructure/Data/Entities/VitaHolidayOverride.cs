using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Vita.Planning.Infrastructure.Data.Entities;

[Table("vita_holiday_overrides", Schema = "core")]
public sealed class VitaHolidayOverride
{
    [Key]
    [Column("vita_holiday_override_id")]
    public int VitaHolidayOverrideId { get; set; }

    [MaxLength(20)]
    [Column("country_code")]
    public string CountryCode { get; set; } = string.Empty;

    [Column("holiday_date")]
    public DateOnly HolidayDate { get; set; }

    [MaxLength(300)]
    [Column("holiday_name")]
    public string? HolidayName { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; }

    [Column("is_half_day")]
    public bool? IsHalfDay { get; set; }

    [Column("hours_reduction")]
    public decimal? HoursReduction { get; set; }

    [MaxLength(60)]
    [Column("holiday_type")]
    public string HolidayType { get; set; } = string.Empty;

    [MaxLength(1000)]
    [Column("notes")]
    public string? Notes { get; set; }

    [Column("created_at_utc")]
    public DateTime CreatedAtUtc { get; set; }

    [Column("updated_at_utc")]
    public DateTime? UpdatedAtUtc { get; set; }
}
