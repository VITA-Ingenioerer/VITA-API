using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Vita.Planning.Infrastructure.Data.Entities;

[Table("project_customers", Schema = "ext")]
public sealed class ExtProjectCustomer
{
    [Key]
    [Column("customer_number")]
    public int CustomerNumber { get; set; }

    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Column("object_version")]
    public string? ObjectVersion { get; set; }

    [Column("source_last_synced_at")]
    public DateTime SourceLastSyncedAt { get; set; }
}