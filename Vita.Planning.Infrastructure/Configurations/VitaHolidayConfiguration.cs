using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vita.Planning.Infrastructure.Data.Entities;

namespace Vita.Planning.Infrastructure.Configurations;

public sealed class VitaHolidayConfiguration : IEntityTypeConfiguration<VitaHoliday>
{
    public void Configure(EntityTypeBuilder<VitaHoliday> builder)
    {
        builder.ToView("vita_holidays", "core");
        builder.HasNoKey();

        builder.Property(x => x.VitaHolidayOverrideId).HasColumnName("vita_holiday_override_id");
        builder.Property(x => x.PublicHolidayCalendarId).HasColumnName("public_holiday_calendar_id");
        builder.Property(x => x.CountryCode).HasColumnName("country_code").HasMaxLength(20).IsRequired();
        builder.Property(x => x.HolidayDate).HasColumnName("holiday_date").HasColumnType("date").IsRequired();
        builder.Property(x => x.HolidayName).HasColumnName("holiday_name").HasMaxLength(300);
        builder.Property(x => x.IsActive).HasColumnName("is_active");
        builder.Property(x => x.IsHalfDay).HasColumnName("is_half_day");
        builder.Property(x => x.HoursReduction).HasColumnName("hours_reduction").HasPrecision(5, 2);
        builder.Property(x => x.HolidayType).HasColumnName("holiday_type").HasMaxLength(60);
        builder.Property(x => x.Notes).HasColumnName("notes").HasMaxLength(1000);
        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
        builder.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc");
    }
}
