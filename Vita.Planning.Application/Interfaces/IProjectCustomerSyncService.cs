using Vita.Planning.Application.DTOs;

namespace Vita.Planning.Application.Interfaces;

public interface IProjectCustomerSyncService
{
    Task<ProjectCustomerSyncResultDto> SyncProjectCustomersAsync(
        string initiatedBy,
        CancellationToken cancellationToken = default);
}