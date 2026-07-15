using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vita.Planning.Infrastructure.Data.Entities;

namespace Vita.Planning.Infrastructure.Configurations;

public sealed class VitaHolidayOverrideConfiguration : IEntityTypeConfiguration<VitaHolidayOverride>
{
    public void Configure(EntityTypeBuilder<VitaHolidayOverride> builder)
    {
        builder.ToTable("vita_holiday_overrides", "core");
        builder.HasKey(x => x.VitaHolidayOverrideId);

        builder.Property(x => x.VitaHolidayOverrideId).HasColumnName("vita_holiday_override_id").ValueGeneratedOnAdd();
        builder.Property(x => x.CountryCode).HasColumnName("country_code").HasMaxLength(20).IsRequired();
        builder.Property(x => x.HolidayDate).HasColumnName("holiday_date").HasColumnType("date").IsRequired();
        builder.Property(x => x.HolidayName).HasColumnName("holiday_name").HasMaxLength(300);
        builder.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(x => x.IsHalfDay).HasColumnName("is_half_day");
        builder.Property(x => x.HoursReduction).HasColumnName("hours_reduction").HasPrecision(5, 2);
        builder.Property(x => x.HolidayType).HasColumnName("holiday_type").HasMaxLength(60).IsRequired();
        builder.Property(x => x.Notes).HasColumnName("notes").HasMaxLength(1000);
        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
        builder.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc");

        builder.HasIndex(x => new { x.CountryCode, x.HolidayDate }).IsUnique();
    }
}
