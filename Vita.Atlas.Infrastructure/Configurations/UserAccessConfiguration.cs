using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vita.Atlas.Infrastructure.Data.Entities;

namespace Vita.Atlas.Infrastructure.Configurations;

public sealed class UserAccessConfiguration : IEntityTypeConfiguration<UserAccess>
{
    public void Configure(EntityTypeBuilder<UserAccess> builder)
    {
        builder.ToTable("user_access", "core");

        // The employee is the key: one grant per person, so a re-grant updates in place and
        // there is no way to end up with two rows disagreeing about someone's role.
        builder.HasKey(x => x.EmployeeId);

        builder.Property(x => x.EmployeeId)
            .HasColumnName("employee_id")
            .ValueGeneratedNever();

        // Stored as the int the enum already declares. The ladder's numbering is part of the
        // contract (Standard 0 < Manager 1 < Admin 2), so persisting it is not a leaked detail.
        builder.Property(x => x.Role)
            .HasColumnName("role")
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.Reason)
            .HasColumnName("reason")
            .HasMaxLength(500);

        builder.Property(x => x.GrantedBy)
            .HasColumnName("granted_by")
            .HasMaxLength(200);

        builder.Property(x => x.GrantedAtUtc)
            .HasColumnName("granted_at_utc")
            .IsRequired();

        builder.Property(x => x.UpdatedBy)
            .HasColumnName("updated_by")
            .HasMaxLength(200);

        builder.Property(x => x.UpdatedAtUtc)
            .HasColumnName("updated_at_utc");

        builder.HasOne(x => x.Employee)
            .WithMany()
            .HasForeignKey(x => x.EmployeeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
