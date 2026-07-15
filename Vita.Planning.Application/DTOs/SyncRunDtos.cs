namespace Vita.Planning.Application.DTOs;

public sealed class SyncRunDto
{
    public long SyncRunId { get; set; }
    public string SourceSystem { get; set; } = string.Empty;
    public string ResourceName { get; set; } = string.Empty;
    public DateTime StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public string Status { get; set; } = string.Empty;
    public int? RowsRead { get; set; }
    public int? RowsInserted { get; set; }
    public int? RowsUpdated { get; set; }
    public int? RowsDeleted { get; set; }
    public int ErrorCount { get; set; }
    public string? InitiatedBy { get; set; }
    public string? Notes { get; set; }
}

public sealed class SyncErrorDto
{
    public long SyncErrorId { get; set; }
    public long SyncRunId { get; set; }
    public string SourceSystem { get; set; } = string.Empty;
    public string ResourceName { get; set; } = string.Empty;
    public string? RecordKey { get; set; }
    public string ErrorStage { get; set; } = string.Empty;
    public string? ErrorCode { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public string? RawPayload { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
