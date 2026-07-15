using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Vita.Planning.Infrastructure.Data.Entities;

[Table("sync_runs", Schema = "ops")]
public sealed class OpsSyncRun
{
    [Key]
    [Column("sync_run_id")]
    public long SyncRunId { get; set; }

    [Column("source_system")]
    public string SourceSystem { get; set; } = string.Empty;

    [Column("resource_name")]
    public string ResourceName { get; set; } = string.Empty;

    [Column("started_at_utc")]
    public DateTime StartedAtUtc { get; set; }

    [Column("completed_at_utc")]
    public DateTime? CompletedAtUtc { get; set; }

    [Column("status")]
    public string Status { get; set; } = string.Empty;

    [Column("rows_read")]
    public int? RowsRead { get; set; }

    [Column("rows_inserted")]
    public int? RowsInserted { get; set; }

    [Column("rows_updated")]
    public int? RowsUpdated { get; set; }

    [Column("rows_deleted")]
    public int? RowsDeleted { get; set; }

    [Column("error_count")]
    public int ErrorCount { get; set; }

    [Column("initiated_by")]
    public string? InitiatedBy { get; set; }

    [Column("notes")]
    public string? Notes { get; set; }

    [Column("correlation_id")]
    public Guid? CorrelationId { get; set; }
}