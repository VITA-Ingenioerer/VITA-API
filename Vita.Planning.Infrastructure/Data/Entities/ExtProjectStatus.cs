using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Vita.Planning.Infrastructure.Data.Entities;

[Table("project_statuses", Schema = "ext")]
public sealed class ExtProjectStatus
{
    [Key]
    [Column("status_number")]
    public int StatusNumber { get; set; }

    [Column("name")]
    public string? Name { get; set; }

    [Column("priority")]
    public int? Priority { get; set; }

    [Column("type_number")]
    public int? TypeNumber { get; set; }

    [Column("object_version")]
    public string? ObjectVersion { get; set; }

    [Column("source_last_synced_at")]
    public DateTime SourceLastSyncedAt { get; set; }
}