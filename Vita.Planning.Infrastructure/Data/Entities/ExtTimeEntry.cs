using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Vita.Planning.Infrastructure.Data.Entities;

[Table("time_entries", Schema = "ext")]
public sealed class ExtTimeEntry
{
    [Key]
    [Column("number")]
    public int Number { get; set; }

    [Column("project_number")]
    public int ProjectNumber { get; set; }

    [Column("activity_number")]
    public int ActivityNumber { get; set; }

    [Column("employee_number")]
    public int EmployeeNumber { get; set; }

    [Column("date")]
    public DateOnly Date { get; set; }

    [Column("text")]
    public string? Text { get; set; }

    [Column("number_of_hours")]
    public decimal? NumberOfHours { get; set; }

    [Column("is_approved")]
    public bool? IsApproved { get; set; }

    [Column("is_reconciled")]
    public bool? IsReconciled { get; set; }

    [Column("last_updated")]
    public DateTime? LastUpdated { get; set; }

    [MaxLength(100)]
    [Column("object_version")]
    public string? ObjectVersion { get; set; }

    [Column("source_last_synced_at")]
    public DateTime SourceLastSyncedAt { get; set; }
}
