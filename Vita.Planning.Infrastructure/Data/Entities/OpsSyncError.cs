using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Vita.Planning.Infrastructure.Data.Entities;

[Table("sync_errors", Schema = "ops")]
public sealed class OpsSyncError
{
    [Key]
    [Column("sync_error_id")]
    public long SyncErrorId { get; set; }

    [Column("sync_run_id")]
    public long SyncRunId { get; set; }

    [Column("source_system")]
    public string SourceSystem { get; set; } = string.Empty;

    [Column("resource_name")]
    public string ResourceName { get; set; } = string.Empty;

    [Column("record_key")]
    public string? RecordKey { get; set; }

    [Column("error_stage")]
    public string ErrorStage { get; set; } = string.Empty;

    [Column("error_code")]
    public string? ErrorCode { get; set; }

    [Column("error_message")]
    public string ErrorMessage { get; set; } = string.Empty;

    [Column("raw_payload")]
    public string? RawPayload { get; set; }

    [Column("created_at_utc")]
    public DateTime CreatedAtUtc { get; set; }

    [ForeignKey(nameof(SyncRunId))]
    public OpsSyncRun? SyncRun { get; set; }
}