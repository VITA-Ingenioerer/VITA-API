using Vita.Atlas.Application.DTOs;

namespace Vita.Atlas.Application.Interfaces;

public interface IProjectSyncService
{
    Task<ProjectSyncResultDto> SyncProjectsAsync(string initiatedBy, CancellationToken cancellationToken = default);
}