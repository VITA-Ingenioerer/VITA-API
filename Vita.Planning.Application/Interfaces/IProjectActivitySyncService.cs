using Vita.Planning.Application.DTOs;

namespace Vita.Planning.Application.Interfaces;

public interface IProjectActivitySyncService
{
    Task<ActivitySyncResultDto> SyncProjectActivitiesAsync(
        string initiatedBy,
        CancellationToken cancellationToken = default);
}
