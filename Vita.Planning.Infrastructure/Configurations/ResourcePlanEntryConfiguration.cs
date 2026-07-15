using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vita.Planning.Infrastructure.Data.Entities;

namespace Vita.Planning.Infrastructure.Configurations;

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

        builder.HasIndex(x => x.PlanningTargetId);
        builder.HasIndex(x => x.PlanDate);
        builder.HasIndex(x => new { x.ResourcePlanId, x.PlanningTargetId, x.PlanDate })
            .IsUnique();

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
