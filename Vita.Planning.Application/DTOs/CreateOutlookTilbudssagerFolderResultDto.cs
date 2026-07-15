namespace Vita.Planning.Application.DTOs;

public sealed class CreateOutlookTilbudssagerFolderResultDto
{
    public int Year { get; init; }

    public string MailboxDisplayName { get; init; } = string.Empty;

    public string MailboxEmail { get; init; } = string.Empty;

    public string FolderDisplayName { get; init; } = string.Empty;

    public string FolderId { get; init; } = string.Empty;

    public string? ParentFolderId { get; init; }

    public int? ChildFolderCount { get; init; }

    public int? UnreadItemCount { get; init; }

    public int? TotalItemCount { get; init; }

    public string FolderLocation { get; init; } = string.Empty;

    public DateTime CreatedAtUtc { get; init; }
}