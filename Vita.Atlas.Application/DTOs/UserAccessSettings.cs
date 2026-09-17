namespace Vita.Atlas.Application.DTOs;

public sealed class UserAccessSettings
{
    /// <summary>
    /// Admins of last resort. These UPNs resolve to Admin regardless of what core.user_access
    /// says, which is what stops the access pane from being able to lock every administrator out
    /// of itself. Deliberately configuration rather than data: recovering from an empty or
    /// mistaken table must not require the table.
    /// </summary>
    public string[] BootstrapAdminUserPrincipalNames { get; set; } = [];
}
