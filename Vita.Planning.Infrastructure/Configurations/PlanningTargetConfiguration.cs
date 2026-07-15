using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vita.Planning.Infrastructure.Data.Entities;

namespace Vita.Planning.Infrastructure.Configurations;

public sealed class PlanningTargetConfiguration : IEntityTypeConfiguration<PlanningTarget>
{
    public void Configure(EntityTypeBuilder<PlanningTarget> builder)
    {
        builder.ToTable("planning_targets", "core");

        builder.HasKey(x => x.PlanningTargetId);

        builder.Property(x => x.PlanningTargetId)
            .HasColumnName("planning_target_id");

        builder.Property(x => x.Code)
            .HasColumnName("code")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.Name)
            .HasColumnName("name")
            .HasMaxLength(510)
            .IsRequired();

        builder.Property(x => x.TargetType)
            .HasColumnName("target_type")
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(x => x.ExtProjectNumber)
            .HasColumnName("ext_project_number");

        builder.Property(x => x.OfferId)
            .HasColumnName("offer_id");

        builder.Property(x => x.InternalPlanningCodeId)
            .HasColumnName("internal_planning_code_id");

        builder.Property(x => x.OfficeCode)
            .HasColumnName("office_code")
            .HasMaxLength(40);

        builder.Property(x => x.IsActive)
            .HasColumnName("is_active")
            .IsRequired();

        builder.Property(x => x.IsPlannable)
            .HasColumnName("is_plannable")
            .IsRequired();

        builder.Property(x => x.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        builder.Property(x => x.UpdatedAtUtc)
            .HasColumnName("updated_at_utc");

        builder.HasIndex(x => new { x.TargetType, x.Code })
            .IsUnique();
    }
}