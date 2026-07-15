using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vita.Planning.Infrastructure.Data.Entities;

namespace Vita.Planning.Infrastructure.Configurations;

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

        builder.HasIndex(x => x.Code);
        builder.HasIndex(x => x.DisciplineId);
    }
}
