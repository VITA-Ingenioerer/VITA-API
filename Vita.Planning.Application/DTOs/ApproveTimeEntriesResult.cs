namespace Vita.Planning.Application.DTOs;

public sealed class ApproveTimeEntriesResult
{
    public IReadOnlyList<int> Approved { get; set; } = Array.Empty<int>();
    public IReadOnlyList<TimeEntryApprovalFailure> Failed { get; set; } = Array.Empty<TimeEntryApprovalFailure>();
}
