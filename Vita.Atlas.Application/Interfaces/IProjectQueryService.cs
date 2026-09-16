using Vita.Atlas.Application.DTOs;

namespace Vita.Atlas.Application.Interfaces;

public interface IProjectQueryService
{
    Task<PagedResultDto<ProjectListItemDto>> GetProjectsAsync(
        int page,
        int pageSize,
        string? query = null,
        bool? isClosed = null,
        bool? isBarred = null,
        string? dawaId = null,
        bool includeLastResourcePlanActivity = false,
        CancellationToken cancellationToken = default);
    Task<ProjectDetailsDto?> GetProjectByNumberAsync(int projectNumber, CancellationToken cancellationToken = default);
}