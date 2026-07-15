using Vita.Planning.Application.DTOs;

namespace Vita.Planning.Application.Interfaces;

public interface IProjectStatusSyncService
{
    Task<ProjectStatusSyncResultDto> SyncProjectStatusesAsync(
        string initiatedBy,
        CancellationToken cancellationToken = default);
}