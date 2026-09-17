using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vita.Atlas.Infrastructure.Data.Entities;

namespace Vita.Atlas.Infrastructure.Configurations;

public sealed class ResourcePlanEntryConfiguration : IEntityTypeConfiguration<ResourcePlanEntry>
{
    public void Configure(EntityTypeBuilder<ResourcePlanEntry> builder)
    {
        builder.ToTable("resource_plan_entries", "core");

        builder.HasKey(x => x.ResourcePlanEntryId);

        builder.Property(x => x.ResourcePlanEntryId)
            .HasColumnName("resource_plan_entry_id");

        builder.Property(x => x.Hours)
            .HasColumnName("hours")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(x => x.Description)
            .HasColumnName("description")
            .HasMaxLength(510);

        builder.Property(x => x.IsManualOverride)
            .HasColumnName("is_manual_override")
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

        builder.Property(x => x.PlanningTargetId)
            .HasColumnName("planning_target_id")
            .IsRequired();

        builder.Property(x => x.PlanDate)
            .HasColumnName("plan_date")
            .HasColumnType("date")
            .IsRequired();

        builder.Property(x => x.ProjectActivityId)
            .HasColumnName("ext_project_activity_number");

        // Covering on purpose. ProjectQueryService/OfferService answer "when was this last
        // planned?" with MAX(updated_at ?? created_at) grouped per target; on the bare
        // planning_target_id index those two columns are off-index, so the aggregate degrades
        // into a key lookup per entry (~270k rows) and the list endpoints pay seconds per page.
        // With them included the group-by is satisfied from the index alone. Matching
        // CREATE INDEX in Sql/2026-09-cover-resource-plan-entry-activity.sql.
        builder.HasIndex(x => x.PlanningTargetId)
            .IncludeProperties(x => new { x.UpdatedAt, x.CreatedAt });
        builder.HasIndex(x => x.PlanDate);

        // Split in two: SQL Server's composite unique index disallows duplicate
        // NULLs, so a single index naively extended with ProjectActivityId would
        // still cap "no activity" entries at one per plan/target/day. See
        // Sql/2026-07-split-entry-uniqueness-by-activity.sql for the matching
        // hand-run migration (this repo has no EF migrations project).
        builder.HasIndex(x => new { x.ResourcePlanId, x.PlanningTargetId, x.PlanDate, x.ProjectActivityId })
            .IsUnique()
            .HasDatabaseName("UX_core_resource_plan_entries_day_activity")
            .HasFilter("[ext_project_activity_number] IS NOT NULL");

        builder.HasIndex(x => new { x.ResourcePlanId, x.PlanningTargetId, x.PlanDate })
            .IsUnique()
            .HasDatabaseName("UX_core_resource_plan_entries_day_no_activity")
            .HasFilter("[ext_project_activity_number] IS NULL");

        builder.HasOne(x => x.PlanningTarget)
            .WithMany()
            .HasForeignKey(x => x.PlanningTargetId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ProjectActivity)
            .WithMany()
            .HasForeignKey(x => x.ProjectActivityId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.ResourcePlan)
            .WithMany()
            .HasForeignKey(x => x.ResourcePlanId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
