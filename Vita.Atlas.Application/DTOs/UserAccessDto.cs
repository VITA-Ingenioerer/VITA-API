using Vita.Atlas.Domain.Enums;

namespace Vita.Atlas.Application.DTOs;

/// <summary>One employee's access, as the access pane shows it.</summary>
public sealed class UserAccessDto
{
    public int EmployeeId { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public string? UserPrincipalName { get; set; }

    public string? Department { get; set; }

    /// <summary>What the user actually gets — the override when there is one, otherwise <see cref="DerivedRole"/>.</summary>
    public AccessRole EffectiveRole { get; set; }

    /// <summary>What the org chart alone would give them, shown so an override's effect is visible.</summary>
    public AccessRole DerivedRole { get; set; }

    /// <summary>True when a row in core.user_access is overriding the derivation.</summary>
    public bool IsOverridden { get; set; }

    /// <summary>Why this person manages nobody yet still has Manager, or vice versa.</summary>
    public string? Reason { get; set; }

    /// <summary>How many active people report to them — the input to <see cref="DerivedRole"/>.</summary>
    public int DirectReportCount { get; set; }

    /// <summary>True when the role comes from the bootstrap allowlist and cannot be edited away.</summary>
    public bool IsProtectedAdmin { get; set; }

    public string? GrantedBy { get; set; }

    public DateTime? GrantedAtUtc { get; set; }
}

/// <summary>What the signed-in caller may do. Drives which web parts the frontend renders.</summary>
public sealed class CurrentUserAccessDto
{
    public int? EmployeeId { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public string? UserPrincipalName { get; set; }

    public AccessRole Role { get; set; }

    /// <summary>
    /// Named capabilities rather than a bare role, so the frontend never has to re-implement the
    /// role ladder. Adding a capability to a role is then a server-side change alone.
    /// </summary>
    public IReadOnlyList<string> Capabilities { get; set; } = [];
}

/// <summary>
/// A role change from the access pane. A null <see cref="Role"/> removes the override and lets
/// the employee fall back to whatever the org chart derives for them.
/// </summary>
public sealed class UpdateUserAccessRequest
{
    public AccessRole? Role { get; set; }

    public string? Reason { get; set; }
}
