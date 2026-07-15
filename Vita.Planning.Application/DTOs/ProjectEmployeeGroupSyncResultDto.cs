namespace Vita.Planning.Application.DTOs;

public sealed class ProjectEmployeeGroupSyncResultDto
{
    public long SyncRunId { get; set; }
    public int RowsRead { get; set; }
    public int RowsInserted { get; set; }
    public int RowsUpdated { get; set; }
    public int ErrorCount { get; set; }
    public string Status { get; set; } = string.Empty;
}