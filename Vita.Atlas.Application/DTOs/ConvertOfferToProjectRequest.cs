using System.ComponentModel.DataAnnotations;

namespace Vita.Atlas.Application.DTOs;

public sealed class ConvertOfferToProjectRequest
{
    public int ProjectGroupNumber { get; set; } = 1;

    public int? ResponsibleEmployeeNumber { get; set; }

    /// <summary>
    /// Overrides the project name used in e-conomic and for sub-project name prefixes.
    /// When omitted, the offer title is used.
    /// </summary>
    [MaxLength(255)]
    public string? ProjectName { get; set; }

    [MaxLength(99)]
    public List<string> SubProjectNames { get; set; } = [];

    [MaxLength(100)]
    public string? ConvertedBy { get; set; }

    /// <summary>
    /// When true, resource plan entries on the offer's planning target that fall on or after
    /// the conversion date are remapped to the destination project's planning target, and the
    /// offer's planning target is deactivated. Entries before the conversion date stay on the
    /// offer so historical planning is not retroactively reattributed.
    /// </summary>
    public bool MigrateResourcePlanEntries { get; set; }

    /// <summary>
    /// Which of the <see cref="SubProjectNames"/> receives the migrated hours, by 0-based
    /// position. Defaults to the first sub-project. Ignored when
    /// <see cref="MigrateToProjectNumber"/> is set, or when no sub-projects are created —
    /// in which case the hours go to the main project.
    /// </summary>
    public int? MigrateToSubProjectIndex { get; set; }

    /// <summary>
    /// Explicit destination project number for the migrated hours. Takes precedence over
    /// <see cref="MigrateToSubProjectIndex"/>. The project must already exist in our mirror
    /// of e-conomic — use this to aim at a project that is not created by this call.
    /// </summary>
    public int? MigrateToProjectNumber { get; set; }

    // Workspace provisioning
    public bool SkipWorkspaceProvisioning { get; set; }
    public bool IsPrivate { get; set; }
    public List<string> MemberUserIds { get; set; } = [];
    public bool CreateTeam { get; set; } = true;

    /// <summary>
    /// UPN (email) of the M365 Group owner. Set by the controller from the caller's JWT.
    /// Falls back to employee 122's UPN, then the DefaultOwnerUserId setting.
    /// </summary>
    public string? OwnerUpn { get; set; }

    /// <summary>
    /// Drive ID of the offer's tilbudssager document library (from the create-folder response).
    /// When provided with OfferSharePointFolderItemId, the economics workbook is copied
    /// from the offer folder into the project SharePoint site.
    /// </summary>
    public string? OfferSharePointDriveId { get; set; }

    /// <summary>
    /// Item ID of the offer's tilbudssager folder (from the create-folder response).
    /// </summary>
    public string? OfferSharePointFolderItemId { get; set; }
}
