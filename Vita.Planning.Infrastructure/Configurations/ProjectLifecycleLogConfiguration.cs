using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vita.Planning.Infrastructure.Data.Entities;

namespace Vita.Planning.Infrastructure.Configurations;

public sealed class ProjectLifecycleLogConfiguration : IEntityTypeConfiguration<ProjectLifecycleLog>
{
    public void Configure(EntityTypeBuilder<ProjectLifecycleLog> builder)
    {
        builder.ToTable("project_lifecycle_log", "core");
        builder.HasKey(x => x.ProjectLifecycleLogId);
        builder.Property(x => x.ProjectLifecycleLogId).HasColumnName("project_lifecycle_log_id").ValueGeneratedOnAdd();
        builder.Property(x => x.TargetType).HasColumnName("target_type").HasMaxLength(20).IsRequired();
        builder.Property(x => x.ProjectNumber).HasColumnName("project_number");
        builder.Property(x => x.OfferId).HasColumnName("offer_id");
        builder.Property(x => x.EventType).HasColumnName("event_type").HasMaxLength(50).IsRequired();
        builder.Property(x => x.EventTitle).HasColumnName("event_title").HasMaxLength(255);
        builder.Property(x => x.EventDescription).HasColumnName("event_description");
        builder.Property(x => x.OldValue).HasColumnName("old_value");
        builder.Property(x => x.NewValue).HasColumnName("new_value");
        builder.Property(x => x.SnapshotJson).HasColumnName("snapshot_json");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by").HasMaxLength(100);
        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();

        builder.HasIndex(x => new { x.TargetType, x.ProjectNumber });
        builder.HasIndex(x => new { x.TargetType, x.OfferId });
        builder.HasIndex(x => x.CreatedAtUtc);
    }
}
