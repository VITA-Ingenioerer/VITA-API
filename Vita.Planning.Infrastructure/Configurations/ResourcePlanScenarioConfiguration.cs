using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vita.Planning.Infrastructure.Data.Entities;

namespace Vita.Planning.Infrastructure.Configurations;

public sealed class ResourcePlanScenarioConfiguration : IEntityTypeConfiguration<ResourcePlanScenario>
{
    public void Configure(EntityTypeBuilder<ResourcePlanScenario> builder)
    {
        builder.ToTable("resource_plan_scenarios", "core");
        builder.HasKey(x => x.ScenarioId);
        builder.Property(x => x.ScenarioId).HasColumnName("scenario_id").ValueGeneratedOnAdd();
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(x => x.Description).HasColumnName("description").HasMaxLength(510);
        builder.Property(x => x.CreatedBy).HasColumnName("created_by").HasMaxLength(200);
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by").HasMaxLength(200);
        builder.Property(x => x.IsDefault).HasColumnName("is_default").IsRequired();
        builder.Property(x => x.IsLocked).HasColumnName("is_locked").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
    }
}
