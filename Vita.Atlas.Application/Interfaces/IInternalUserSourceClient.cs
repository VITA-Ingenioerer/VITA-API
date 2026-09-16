using Vita.Atlas.Application.DTOs;

namespace Vita.Atlas.Application.Interfaces;

public interface IInternalUserSourceClient
{
    Task<IReadOnlyList<SourceUserDto>> GetUsersAsync(CancellationToken cancellationToken = default);
}