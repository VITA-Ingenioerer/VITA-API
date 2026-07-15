using Vita.Planning.Application.DTOs;

namespace Vita.Planning.Application.Interfaces;

public interface IProjectLifecycleLogService
{
    Task<PagedResultDto<ProjectLifecycleLogDto>> GetAllAsync(
        string? targetType = null,
        int? projectNumber = null,
        int? offerId = null,
        string? eventType = null,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default);

    Task<ProjectLifecycleLogDto?> GetByIdAsync(long id, CancellationToken cancellationToken = default);

    Task<ProjectLifecycleLogDto> CreateAsync(CreateProjectLifecycleLogRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProjectLifecycleLogDto>> GetByCorrelationIdAsync(Guid correlationId, CancellationToken cancellationToken = default);
}
