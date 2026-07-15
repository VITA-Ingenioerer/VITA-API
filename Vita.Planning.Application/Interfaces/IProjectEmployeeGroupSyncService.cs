using Vita.Planning.Application.DTOs;

namespace Vita.Planning.Application.Interfaces;

public interface IProjectEmployeeGroupSyncService
{
    Task<ProjectEmployeeGroupSyncResultDto> SyncProjectEmployeeGroupsAsync(
        string initiatedBy,
        CancellationToken cancellationToken = default);
}