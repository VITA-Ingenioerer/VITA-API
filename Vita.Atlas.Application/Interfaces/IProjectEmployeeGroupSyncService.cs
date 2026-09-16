using Vita.Atlas.Application.DTOs;

namespace Vita.Atlas.Application.Interfaces;

public interface IProjectEmployeeGroupSyncService
{
    Task<ProjectEmployeeGroupSyncResultDto> SyncProjectEmployeeGroupsAsync(
        string initiatedBy,
        CancellationToken cancellationToken = default);
}