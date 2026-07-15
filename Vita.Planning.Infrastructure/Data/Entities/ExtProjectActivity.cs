using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Vita.Planning.Infrastructure.Data.Entities;

[Table("project_activities", Schema = "ext")]
public sealed class ExtProjectActivity
{
    [Key]
    [Column("number")]
    public int Number { get; set; }

    [Column("project_number")]
    public int ProjectNumber { get; set; }

    [Column("activity_number")]
    public int ActivityNumber { get; set; }

    [Column("start_date")]
    public DateTime? StartDate { get; set; }

    [Column("end_date")]
    public DateTime? EndDate { get; set; }

    [Column("responsible_employee_number")]
    public int? ResponsibleEmployeeNumber { get; set; }

    [Column("completed")]
    public bool Completed { get; set; }

    [Column("object_version")]
    public string? ObjectVersion { get; set; }

    [Column("last_updated_utc")]
    public DateTime? LastUpdatedUtc { get; set; }

    [Column("source_last_synced_at")]
    public DateTime? SourceLastSyncedAt { get; set; }

    public ExtActivity? Activity { get; set; }
}
