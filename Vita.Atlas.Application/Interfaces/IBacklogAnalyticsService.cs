using Vita.Atlas.Application.DTOs;

namespace Vita.Atlas.Application.Interfaces;

public interface IBacklogAnalyticsService
{
    Task<BacklogAnalyticsDto> GetBacklogAsync(
        BacklogAnalyticsFilterRequest? filter,
        CancellationToken cancellationToken = default);
}
