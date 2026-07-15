using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vita.Planning.Infrastructure.Data.Entities;

namespace Vita.Planning.Infrastructure.Configurations;

public sealed class ResourcePlanSnapshotConfiguration : IEntityTypeConfiguration<ResourcePlanSnapshot>
{
    public void Configure(EntityTypeBuilder<ResourcePlanSnapshot> builder)
    {
        builder.ToTable("resource_plan_snapshots", "core");
        builder.HasKey(x => x.ResourcePlanSnapshotId);
        builder.Property(x => x.ResourcePlanSnapshotId).HasColumnName("resource_plan_snapshot_id").ValueGeneratedOnAdd();
        builder.Property(x => x.ScenarioId).HasColumnName("scenario_id").IsRequired();
        builder.Property(x => x.SnapshotName).HasColumnName("snapshot_name").HasMaxLength(200);
        builder.Property(x => x.SnapshotType).HasColumnName("snapshot_type").HasMaxLength(30).IsRequired();
        builder.Property(x => x.SnapshotAsOfUtc).HasColumnName("snapshot_as_of_utc").IsRequired();
        builder.Property(x => x.CreatedBy).HasColumnName("created_by").HasMaxLength(100);
        builder.Property(x => x.Notes).HasColumnName("notes").HasMaxLength(1000);
        builder.HasIndex(x => new { x.ScenarioId, x.SnapshotAsOfUtc });
    }
}

public sealed class ResourcePlanSnapshotEntryConfiguration : IEntityTypeConfiguration<ResourcePlanSnapshotEntry>
{
    public void Configure(EntityTypeBuilder<ResourcePlanSnapshotEntry> builder)
    {
        builder.ToTable("resource_plan_snapshot_entries", "core");
        builder.HasKey(x => x.ResourcePlanSnapshotEntryId);
        builder.Property(x => x.ResourcePlanSnapshotEntryId).HasColumnName("resource_plan_snapshot_entry_id").ValueGeneratedOnAdd();
        builder.Property(x => x.ResourcePlanSnapshotId).HasColumnName("resource_plan_snapshot_id").IsRequired();
        builder.Property(x => x.ResourcePlanId).HasColumnName("resource_plan_id").IsRequired();
        builder.Property(x => x.ResourcePlanEntryId).HasColumnName("resource_plan_entry_id");
        builder.Property(x => x.EmployeeId).HasColumnName("employee_id").IsRequired();
        builder.Property(x => x.ScenarioId).HasColumnName("scenario_id").IsRequired();
        builder.Property(x => x.PlanningTargetId).HasColumnName("planning_target_id");
        builder.Property(x => x.ProjectNumber).HasColumnName("project_number");
        builder.Property(x => x.PlanningCode).HasColumnName("planning_code").HasMaxLength(30);
        builder.Property(x => x.DisplayText).HasColumnName("display_text").HasMaxLength(150);
        builder.Property(x => x.YearNumber).HasColumnName("year_number").IsRequired();
        builder.Property(x => x.MonthNumber).HasColumnName("month_number");
        builder.Property(x => x.WeekNumber).HasColumnName("week_number");
        builder.Property(x => x.PeriodType).HasColumnName("period_type").HasMaxLength(20).IsRequired();
        builder.Property(x => x.Hours).HasColumnName("hours").HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.Description).HasColumnName("description").HasMaxLength(255);
        builder.Property(x => x.IsManualOverride).HasColumnName("is_manual_override").IsRequired();
        builder.Property(x => x.SnapshotAsOfUtc).HasColumnName("snapshot_as_of_utc").IsRequired();
        builder.HasIndex(x => x.ResourcePlanSnapshotId);
        builder.HasIndex(x => new { x.ScenarioId, x.EmployeeId, x.PlanningTargetId });
    }
}
