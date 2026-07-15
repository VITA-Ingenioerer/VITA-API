using Vita.Planning.Application.DTOs;

namespace Vita.Planning.Application.Interfaces;

public interface IUserSyncService
{
    Task<UserSyncResultDto> SyncUsersAsync(string initiatedBy, CancellationToken cancellationToken = default);
}