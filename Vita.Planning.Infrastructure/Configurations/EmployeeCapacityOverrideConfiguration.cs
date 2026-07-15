using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vita.Planning.Infrastructure.Data.Entities;

namespace Vita.Planning.Infrastructure.Configurations;

public sealed class EmployeeCapacityOverrideConfiguration : IEntityTypeConfiguration<EmployeeCapacityOverride>
{
    public void Configure(EntityTypeBuilder<EmployeeCapacityOverride> builder)
    {
        builder.ToTable("employee_capacity_overrides", "core");
        builder.HasKey(x => x.EmployeeCapacityOverrideId);
        builder.Property(x => x.EmployeeCapacityOverrideId).HasColumnName("employee_capacity_override_id").ValueGeneratedOnAdd();
        builder.Property(x => x.EmployeeId).HasColumnName("employee_id").IsRequired();
        builder.Property(x => x.EffectiveFrom).HasColumnName("effective_from").IsRequired();
        builder.Property(x => x.EffectiveTo).HasColumnName("effective_to");
        builder.Property(x => x.OverrideType).HasColumnName("override_type").HasMaxLength(60).IsRequired();
        builder.Property(x => x.WeeklyHours).HasColumnName("weekly_hours").HasPrecision(5, 2);
        builder.Property(x => x.CapacityFactor).HasColumnName("capacity_factor").HasPrecision(6, 4);
        builder.Property(x => x.FixedHoursPerWeek).HasColumnName("fixed_hours_per_week").HasPrecision(5, 2);
        builder.Property(x => x.Reason).HasColumnName("reason").HasMaxLength(500);
        builder.Property(x => x.Notes).HasColumnName("notes").HasMaxLength(2000);
        builder.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(x => x.MondayHours).HasColumnName("monday_hours").HasPrecision(5, 2);
        builder.Property(x => x.TuesdayHours).HasColumnName("tuesday_hours").HasPrecision(5, 2);
        builder.Property(x => x.WednesdayHours).HasColumnName("wednesday_hours").HasPrecision(5, 2);
        builder.Property(x => x.ThursdayHours).HasColumnName("thursday_hours").HasPrecision(5, 2);
        builder.Property(x => x.FridayHours).HasColumnName("friday_hours").HasPrecision(5, 2);
        builder.Property(x => x.SaturdayHours).HasColumnName("saturday_hours").HasPrecision(5, 2);
        builder.Property(x => x.SundayHours).HasColumnName("sunday_hours").HasPrecision(5, 2);
        builder.Property(x => x.CreatedBy).HasColumnName("created_by").HasMaxLength(200);
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by").HasMaxLength(200);
        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
        builder.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc");
    }
}
