using System.ComponentModel.DataAnnotations;

namespace Vita.Planning.Application.DTOs;

public sealed class CreateProjectRequest
{
    [Required]
    [MaxLength(255)]
    public string Name { get; set; } = string.Empty;

    public int ProjectGroupNumber { get; set; } = 1;

    [Required]
    public int CustomerId { get; set; }

    public int? ResponsibleEmployeeNumber { get; set; }

    [MaxLength(99)]
    public List<string> SubProjectNames { get; set; } = [];

    public List<ProjectPartnerRequest> Partners { get; set; } = [];

    [MaxLength(100)]
    public string? CreatedBy { get; set; }

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
}
