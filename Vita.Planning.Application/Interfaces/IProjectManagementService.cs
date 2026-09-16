using Vita.Planning.Application.DTOs;

namespace Vita.Planning.Application.Interfaces;

public interface IProjectManagementService
{
    Task<CreateProjectResult> CreateProjectAsync(CreateProjectRequest request, CancellationToken cancellationToken = default);
    Task UpdateProjectPartnersAsync(int projectNumber, IReadOnlyList<ProjectPartnerRequest> partners, string? updatedBy = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds one sub-project to an existing main project, in e-conomic and in our own
    /// mirror. Offers have no sub-projects — this is projects only.
    /// </summary>
    Task<AddSubProjectResult> AddSubProjectAsync(
        int mainProjectNumber,
        AddSubProjectRequest request,
        CancellationToken cancellationToken = default);
}
