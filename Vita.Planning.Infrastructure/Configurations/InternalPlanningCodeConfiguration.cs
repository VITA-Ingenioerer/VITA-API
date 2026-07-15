using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vita.Planning.Infrastructure.Data.Entities;

namespace Vita.Planning.Infrastructure.Configurations;

public sealed class InternalPlanningCodeConfiguration : IEntityTypeConfiguration<InternalPlanningCode>
{
    public void Configure(EntityTypeBuilder<InternalPlanningCode> builder)
    {
        builder.ToTable("internal_planning_codes", "core");

        builder.HasKey(x => x.InternalPlanningCodeId);

        builder.Property(x => x.InternalPlanningCodeId)
            .HasColumnName("internal_planning_code_id");

        builder.Property(x => x.Code)
            .HasColumnName("code")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.Name)
            .HasColumnName("name")
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(x => x.Category)
            .HasColumnName("category")
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(x => x.OfficeCode)
            .HasColumnName("office_code")
            .HasMaxLength(20);

        builder.Property(x => x.DefaultDescription)
            .HasColumnName("default_description")
            .HasMaxLength(255);

        builder.Property(x => x.ColorTag)
            .HasColumnName("color_tag")
            .HasMaxLength(20);

        builder.Property(x => x.IsActive)
            .HasColumnName("is_active");

        builder.Property(x => x.IsPlannable)
            .HasColumnName("is_plannable");

        builder.Property(x => x.IsAbsence)
            .HasColumnName("is_absence");

        builder.Property(x => x.IsInternal)
            .HasColumnName("is_internal");

        builder.Property(x => x.IsBillable)
            .HasColumnName("is_billable");

        builder.Property(x => x.CreatedBy)
            .HasColumnName("created_by")
            .HasMaxLength(100);

        builder.Property(x => x.UpdatedBy)
            .HasColumnName("updated_by")
            .HasMaxLength(100);

        builder.Property(x => x.CreatedAtUtc)
            .HasColumnName("created_at_utc");

        builder.Property(x => x.UpdatedAtUtc)
            .HasColumnName("updated_at_utc");

        builder.HasIndex(x => x.Code)
            .IsUnique();

        builder.HasIndex(x => new { x.Category, x.OfficeCode, x.IsActive });
    }
}