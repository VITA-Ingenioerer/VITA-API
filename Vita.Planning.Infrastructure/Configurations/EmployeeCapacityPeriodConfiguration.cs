using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vita.Planning.Infrastructure.Data.Entities;

namespace Vita.Planning.Infrastructure.Configurations;

public sealed class EmployeeCapacityPeriodConfiguration : IEntityTypeConfiguration<EmployeeCapacityPeriod>
{
    public void Configure(EntityTypeBuilder<EmployeeCapacityPeriod> builder)
    {
        builder.ToTable("employee_capacity_periods", "core");
        builder.HasKey(x => x.EmployeeCapacityPeriodId);
        builder.Property(x => x.EmployeeCapacityPeriodId).HasColumnName("employee_capacity_period_id").ValueGeneratedOnAdd();
        builder.Property(x => x.EmployeeId).HasColumnName("employee_id").IsRequired();
        builder.Property(x => x.PeriodStart).HasColumnName("period_start").IsRequired();
        builder.Property(x => x.PeriodEnd).HasColumnName("period_end").IsRequired();
        builder.Property(x => x.PeriodType).HasColumnName("period_type").HasMaxLength(20).IsRequired();
        builder.Property(x => x.WeeklyHoursBasis).HasColumnName("weekly_hours_basis").HasPrecision(5, 2);
        builder.Property(x => x.PublicHolidayDays).HasColumnName("public_holiday_days").HasPrecision(5, 2);
        builder.Property(x => x.CapacityHours).HasColumnName("capacity_hours").HasPrecision(8, 2);
        builder.Property(x => x.IsGenerated).HasColumnName("is_generated").IsRequired();
        builder.Property(x => x.GenerationSource).HasColumnName("generation_source").HasMaxLength(60).IsRequired();
        builder.Property(x => x.GeneratedAtUtc).HasColumnName("generated_at_utc").IsRequired();
    }
}
