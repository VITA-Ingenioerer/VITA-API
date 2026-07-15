using Vita.Planning.Application.DTOs;

namespace Vita.Planning.Application.Interfaces;

public interface IProjectSyncService
{
    Task<ProjectSyncResultDto> SyncProjectsAsync(string initiatedBy, CancellationToken cancellationToken = default);
}