using Vita.Planning.Application.DTOs;

namespace Vita.Planning.Application.Interfaces;

public interface IEconomicProjectGroupSourceClient
{
    Task<IReadOnlyList<SourceEconomicProjectGroupDto>> GetProjectGroupsAsync(CancellationToken cancellationToken = default);
}