namespace Vita.Planning.Application.DTOs;

public sealed class ProjectWorkspaceProvisioningResult
{
    public string? GroupId { get; set; }
    public string? SiteId { get; set; }
    public string? DriveId { get; set; }
    public string? SharePointSiteUrl { get; set; }
    public string? TeamsId { get; set; }
    public string? OutlookFolderId { get; set; }
    public bool OfferFolderCopied { get; set; }
    public bool WorkbookCopied { get; set; }
    public List<string> Warnings { get; set; } = [];
    public bool HasWarnings => Warnings.Count > 0;
}
