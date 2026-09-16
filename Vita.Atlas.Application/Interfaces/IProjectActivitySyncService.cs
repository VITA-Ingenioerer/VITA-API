using Vita.Atlas.Application.DTOs;

namespace Vita.Atlas.Application.Interfaces;

public interface IProjectActivitySyncService
{
    Task<ActivitySyncResultDto> SyncProjectActivitiesAsync(
        string initiatedBy,
        CancellationToken cancellationToken = default);
}
