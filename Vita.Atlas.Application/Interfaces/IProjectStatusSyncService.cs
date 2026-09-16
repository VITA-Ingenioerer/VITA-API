using Vita.Atlas.Application.DTOs;

namespace Vita.Atlas.Application.Interfaces;

public interface IProjectStatusSyncService
{
    Task<ProjectStatusSyncResultDto> SyncProjectStatusesAsync(
        string initiatedBy,
        CancellationToken cancellationToken = default);
}