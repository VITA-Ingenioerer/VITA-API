using Vita.Atlas.Application.DTOs;

namespace Vita.Atlas.Application.Interfaces;

public interface IProjectCustomerSyncService
{
    Task<ProjectCustomerSyncResultDto> SyncProjectCustomersAsync(
        string initiatedBy,
        CancellationToken cancellationToken = default);
}