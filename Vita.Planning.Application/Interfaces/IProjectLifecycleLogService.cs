using Vita.Planning.Application.DTOs;

namespace Vita.Planning.Application.Interfaces;

public interface IProjectLifecycleLogService
{
    Task<IReadOnlyList<ProjectLifecycleLogDto>> GetAllAsync(
        string? targetType = null,
        int? projectNumber = null,
        int? offerId = null,
        string? eventType = null,
        CancellationToken cancellationToken = default);

    Task<ProjectLifecycleLogDto?> GetByIdAsync(long id, CancellationToken cancellationToken = default);

    Task<ProjectLifecycleLogDto> CreateAsync(CreateProjectLifecycleLogRequest request, CancellationToken cancellationToken = default);
}
