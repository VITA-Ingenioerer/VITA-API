namespace Vita.Planning.Application.DTOs;

public sealed class ProjectWorkspaceProvisioningRequest
{
    public int MainProjectNumber { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public bool IsPrivate { get; set; }
    public List<string> MemberUserIds { get; set; } = [];
    public bool CreateTeam { get; set; } = true;

    /// <summary>
    /// AAD object ID or UPN of the M365 Group owner.
    /// Resolved by the service layer: caller OID → employee 122 UPN → settings fallback.
    /// </summary>
    public string? OwnerAadIdentifier { get; set; }

    /// <summary>
    /// Drive ID of the offer's tilbudssager document library. When set together with
    /// OfferSharePointFolderItemId, the economics workbook is copied from the offer
    /// folder into the project site.
    /// </summary>
    public string? OfferSharePointDriveId { get; set; }

    /// <summary>
    /// Item ID of the offer's tilbudssager folder in SharePoint.
    /// </summary>
    public string? OfferSharePointFolderItemId { get; set; }

    /// <summary>
    /// Display name/identifier of who triggered this provisioning run, for the
    /// per-step project_lifecycle_log entries written by the client.
    /// </summary>
    public string? InitiatedBy { get; set; }
}
