using Vita.Atlas.Application.DTOs;

namespace Vita.Atlas.Application.Interfaces;

public interface IProjectMetadataService
{
    Task<IReadOnlyList<ProjectMetadataDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<ProjectMetadataDto?> GetByProjectNumberAsync(int projectNumber, CancellationToken cancellationToken = default);
    Task<ProjectMetadataDto> UpsertForProjectAsync(
        int projectNumber,
        UpsertProjectMetadataRequest request,
        CallerInfo caller,
        CancellationToken cancellationToken = default);
}
