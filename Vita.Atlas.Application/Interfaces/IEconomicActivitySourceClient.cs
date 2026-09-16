using Vita.Atlas.Application.DTOs;

namespace Vita.Atlas.Application.Interfaces;

public interface IEconomicActivitySourceClient
{
    Task<IReadOnlyList<SourceEconomicActivityDto>> GetActivitiesAsync(
        CancellationToken cancellationToken = default);
}