using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Vita.Planning.Infrastructure.Data.Entities;

[Table("project_employee_groups", Schema = "ext")]
public sealed class ExtProjectEmployeeGroup
{
    [Key]
    [Column("employee_group_number")]
    public int EmployeeGroupNumber { get; set; }

    [Column("name")]
    public string? Name { get; set; }

    [Column("object_version")]
    public string? ObjectVersion { get; set; }

    [Column("source_last_synced_at")]
    public DateTime SourceLastSyncedAt { get; set; }
}