using Vita.Atlas.Application.DTOs;

namespace Vita.Atlas.Application.Interfaces;

public interface IActivitySyncService
{
    Task<ActivitySyncResultDto> SyncActivitiesAsync(
        string initiatedBy,
        CancellationToken cancellationToken = default);
}