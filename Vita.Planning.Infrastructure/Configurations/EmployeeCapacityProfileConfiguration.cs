using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vita.Planning.Infrastructure.Data.Entities;

namespace Vita.Planning.Infrastructure.Configurations;

public sealed class EmployeeCapacityProfileConfiguration : IEntityTypeConfiguration<EmployeeCapacityProfile>
{
    public void Configure(EntityTypeBuilder<EmployeeCapacityProfile> builder)
    {
        builder.ToTable("employee_capacity_profiles", "core");
        builder.HasKey(x => x.EmployeeCapacityProfileId);
        builder.Property(x => x.EmployeeCapacityProfileId).HasColumnName("employee_capacity_profile_id").ValueGeneratedOnAdd();
        builder.Property(x => x.EmployeeId).HasColumnName("employee_id").IsRequired();
        builder.Property(x => x.EffectiveFrom).HasColumnName("effective_from").IsRequired();
        builder.Property(x => x.EffectiveTo).HasColumnName("effective_to");
        builder.Property(x => x.DefaultWeeklyHours).HasColumnName("default_weekly_hours").HasPrecision(5, 2).IsRequired();
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

        // Filtered on every create/update overlap check today (RequireNoOverlappingActivePeriodAsync)
        // with no supporting index — this already costs something now, not just at scale.
        // Requires the matching CREATE INDEX in the hand-run SQL script — see
        // Sql/2026-08-add-catalog-filter-indexes.sql.
        builder.HasIndex(x => x.EmployeeId);
    }
}
