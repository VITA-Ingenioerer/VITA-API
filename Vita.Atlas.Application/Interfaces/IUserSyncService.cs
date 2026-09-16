using Vita.Atlas.Application.DTOs;

namespace Vita.Atlas.Application.Interfaces;

public interface IUserSyncService
{
    Task<UserSyncResultDto> SyncUsersAsync(string initiatedBy, CancellationToken cancellationToken = default);
}