using Vita.Planning.Application.DTOs;

namespace Vita.Planning.Application.Interfaces;

public interface IProjectManagementService
{
    Task<CreateProjectResult> CreateProjectAsync(CreateProjectRequest request, CancellationToken cancellationToken = default);
    Task UpdateProjectPartnersAsync(int projectNumber, IReadOnlyList<ProjectPartnerRequest> partners, string? updatedBy = null, CancellationToken cancellationToken = default);
}
