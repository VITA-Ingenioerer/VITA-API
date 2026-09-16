using Vita.Atlas.Application.DTOs;

namespace Vita.Atlas.Application.Interfaces;

public interface IProjectGroupSyncService
{
    Task<ProjectGroupSyncResultDto> SyncProjectGroupsAsync(string initiatedBy, CancellationToken cancellationToken = default);
}