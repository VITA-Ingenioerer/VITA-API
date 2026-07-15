using Vita.Planning.Application.DTOs;

namespace Vita.Planning.Application.Interfaces;

public interface IProjectEmployeeSyncService
{
    Task<ProjectEmployeeSyncResultDto> SyncProjectEmployeesAsync(
        string initiatedBy,
        CancellationToken cancellationToken = default);
}