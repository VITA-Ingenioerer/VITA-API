using Vita.Planning.Application.DTOs;

namespace Vita.Planning.Application.Interfaces;

public interface IProjectQueryService
{
    Task<PagedResultDto<ProjectListItemDto>> GetProjectsAsync(
        int page,
        int pageSize,
        string? query = null,
        bool? isClosed = null,
        bool? isBarred = null,
        string? dawaId = null,
        CancellationToken cancellationToken = default);
    Task<ProjectDetailsDto?> GetProjectByNumberAsync(int projectNumber, CancellationToken cancellationToken = default);
}