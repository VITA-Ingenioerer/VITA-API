using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vita.Atlas.Infrastructure.Data.Entities;

namespace Vita.Atlas.Infrastructure.Configurations;

public sealed class VirtualResourceConfiguration : IEntityTypeConfiguration<VirtualResource>
{
    public void Configure(EntityTypeBuilder<VirtualResource> builder)
    {
        builder.ToTable("virtual_resources", "core");

        builder.HasKey(x => x.VirtualResourceId);

        builder.Property(x => x.VirtualResourceId)
            .HasColumnName("virtual_resource_id");

        builder.Property(x => x.Code)
            .HasColumnName("code")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.Name)
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.DisciplineId)
            .HasColumnName("discipline_id");

        builder.Property(x => x.IsActive)
            .HasColumnName("is_active")
            .IsRequired();

        builder.Property(x => x.CustomerId)
            .HasColumnName("customer_id");

        builder.HasOne(x => x.Customer)
            .WithMany()
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        // Unique: the legacy Timer-tabel import matches a row's initials against this code, so
        // two resources sharing one would make that lookup ambiguous.
        builder.HasIndex(x => x.Code)
            .IsUnique()
            .HasDatabaseName("UX_core_virtual_resources_code");

        builder.HasIndex(x => x.DisciplineId);

        builder.HasIndex(x => x.CustomerId)
            .HasFilter("[customer_id] IS NOT NULL");
    }
}
