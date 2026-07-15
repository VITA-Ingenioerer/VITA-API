using Vita.Planning.Application.DTOs;

namespace Vita.Planning.Application.Interfaces;

public interface IProjectGroupSyncService
{
    Task<ProjectGroupSyncResultDto> SyncProjectGroupsAsync(string initiatedBy, CancellationToken cancellationToken = default);
}