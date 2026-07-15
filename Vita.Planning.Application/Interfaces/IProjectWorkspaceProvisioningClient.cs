using Vita.Planning.Application.DTOs;

namespace Vita.Planning.Application.Interfaces;

public interface IProjectWorkspaceProvisioningClient
{
    Task<ProjectWorkspaceProvisioningResult> ProvisionAsync(
        ProjectWorkspaceProvisioningRequest request,
        CancellationToken cancellationToken = default);

    Task<(string? siteId, string? driveId, string? siteWebUrl)> TryGetGroupSiteAsync(
        string groupId,
        CancellationToken cancellationToken = default);
}
