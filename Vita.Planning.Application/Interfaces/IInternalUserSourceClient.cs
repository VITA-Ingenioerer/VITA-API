using Vita.Planning.Application.DTOs;

namespace Vita.Planning.Application.Interfaces;

public interface IInternalUserSourceClient
{
    Task<IReadOnlyList<SourceUserDto>> GetUsersAsync(CancellationToken cancellationToken = default);
}