using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vita.Planning.Infrastructure.Data.Entities;

namespace Vita.Planning.Infrastructure.Configurations;

public sealed class PublicHolidayCalendarConfiguration : IEntityTypeConfiguration<PublicHolidayCalendar>
{
    public void Configure(EntityTypeBuilder<PublicHolidayCalendar> builder)
    {
        builder.ToTable("public_holiday_calendar", "core");
        builder.HasKey(x => x.PublicHolidayCalendarId);
        builder.Property(x => x.PublicHolidayCalendarId).HasColumnName("public_holiday_calendar_id").ValueGeneratedOnAdd();
        builder.Property(x => x.CountryCode).HasColumnName("country_code").HasMaxLength(20).IsRequired();
        builder.Property(x => x.HolidayDate).HasColumnName("holiday_date").IsRequired();
        builder.Property(x => x.HolidayName).HasColumnName("holiday_name").HasMaxLength(300).IsRequired();
        builder.Property(x => x.IsPublicHoliday).HasColumnName("is_public_holiday").IsRequired();
        builder.Property(x => x.IsHalfDay).HasColumnName("is_half_day").IsRequired();
        builder.Property(x => x.HoursReduction).HasColumnName("hours_reduction").HasPrecision(5, 2);
        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
    }
}
