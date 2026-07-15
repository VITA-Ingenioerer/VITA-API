using Vita.Planning.Application.DTOs;

namespace Vita.Planning.Application.Interfaces;

public interface IActivitySyncService
{
    Task<ActivitySyncResultDto> SyncActivitiesAsync(
        string initiatedBy,
        CancellationToken cancellationToken = default);
}