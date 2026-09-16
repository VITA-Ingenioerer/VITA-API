using Vita.Atlas.Application.DTOs;

namespace Vita.Atlas.Application.Interfaces;

public interface IEconomicProjectGroupSourceClient
{
    Task<IReadOnlyList<SourceEconomicProjectGroupDto>> GetProjectGroupsAsync(CancellationToken cancellationToken = default);
}