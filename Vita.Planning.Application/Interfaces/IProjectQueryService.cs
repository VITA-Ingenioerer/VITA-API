using Vita.Planning.Application.DTOs;

namespace Vita.Planning.Application.Interfaces;

public interface IProjectQueryService
{
    Task<PagedResultDto<ProjectListItemDto>> GetProjectsAsync(int page, int pageSize, string? query = null, CancellationToken cancellationToken = default);
    Task<ProjectDetailsDto?> GetProjectByNumberAsync(int projectNumber, CancellationToken cancellationToken = default);
}