using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vita.Planning.Infrastructure.Data.Entities;

namespace Vita.Planning.Infrastructure.Configurations;

public sealed class ResourcePlanConfiguration : IEntityTypeConfiguration<ResourcePlan>
{
    public void Configure(EntityTypeBuilder<ResourcePlan> builder)
    {
        builder.ToTable("resource_plans", "core");

        builder.HasKey(x => x.ResourcePlanId);

        builder.Property(x => x.ResourcePlanId)
            .HasColumnName("resource_plan_id");

        builder.Property(x => x.EmployeeId)
            .HasColumnName("employee_id");

        builder.Property(x => x.VirtualResourceId)
            .HasColumnName("virtual_resource_id");

        builder.Property(x => x.ScenarioId)
            .HasColumnName("scenario_id")
            .IsRequired();

        builder.Property(x => x.StartYear)
            .HasColumnName("start_year")
            .IsRequired();

        builder.Property(x => x.StartMonth)
            .HasColumnName("start_month")
            .IsRequired();

        builder.Property(x => x.VisibleMonths)
            .HasColumnName("visible_months")
            .IsRequired();

        builder.Property(x => x.Notes)
            .HasColumnName("notes")
            .HasMaxLength(1000);

        builder.Property(x => x.IsActive)
            .HasColumnName("is_active")
            .IsRequired();

        builder.Property(x => x.CreatedBy)
            .HasColumnName("created_by")
            .HasMaxLength(200);

        builder.Property(x => x.UpdatedBy)
            .HasColumnName("updated_by")
            .HasMaxLength(200);

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at");

        // Mirrors IX_core_resource_plans_user_scenario / IX_core_resource_plans_virtual_scenario
        // in the DB (both non-unique, no duplicate-prevention behind them).
        builder.HasIndex(x => new { x.EmployeeId, x.ScenarioId, x.IsActive })
            .HasDatabaseName("IX_core_resource_plans_user_scenario");

        builder.HasIndex(x => new { x.VirtualResourceId, x.ScenarioId, x.IsActive })
            .HasDatabaseName("IX_core_resource_plans_virtual_scenario");

        builder.HasIndex(x => x.ScenarioId);
    }
}