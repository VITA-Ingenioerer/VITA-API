namespace Vita.Atlas.Application.Interfaces;

/// <summary>
/// Writes the handful of Entra user fields VITA maintains itself — department, office and
/// manager. Entra stays the system of record: our own ext.users row mirrors what was written
/// so the UI is correct before the next sync, it does not become a second source of truth.
/// </summary>
public interface IEntraUserProfileClient
{
    /// <summary>
    /// Sets department and office location. A null value clears the field in Entra; passing
    /// the same value back is a no-op write.
    /// </summary>
    Task UpdateProfileAsync(
        string userPrincipalName,
        string? department,
        string? officeLocation,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Points the user's manager at <paramref name="managerUserPrincipalName"/>, or removes
    /// the manager entirely when it is null. Separate from the profile write because Graph
    /// models the manager as a relationship, not a property.
    /// </summary>
    Task UpdateManagerAsync(
        string userPrincipalName,
        string? managerUserPrincipalName,
        CancellationToken cancellationToken = default);
}
