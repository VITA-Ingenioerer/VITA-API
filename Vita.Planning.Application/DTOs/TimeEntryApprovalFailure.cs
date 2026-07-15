namespace Vita.Planning.Application.DTOs;

public sealed class TimeEntryApprovalFailure
{
    public int Number { get; set; }
    public DateTime? Date { get; set; }
    public double? Hours { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? ErrorCode { get; set; }
}
