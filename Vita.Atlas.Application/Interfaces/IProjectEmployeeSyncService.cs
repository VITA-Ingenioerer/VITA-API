using Vita.Atlas.Application.DTOs;

namespace Vita.Atlas.Application.Interfaces;

public interface IProjectEmployeeSyncService
{
    Task<ProjectEmployeeSyncResultDto> SyncProjectEmployeesAsync(
        string initiatedBy,
        CancellationToken cancellationToken = default);
}