namespace Vita.Planning.Application.DTOs;

public sealed class OvertimeBalanceRefreshResultDto
{
    public long SyncRunId { get; set; }
    public int EmployeesProcessed { get; set; }
    public int EmployeesFailed { get; set; }
    public int RowsWritten { get; set; }
    public string Status { get; set; } = string.Empty;
}
