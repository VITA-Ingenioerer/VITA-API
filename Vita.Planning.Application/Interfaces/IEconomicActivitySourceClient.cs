using Vita.Planning.Application.DTOs;

namespace Vita.Planning.Application.Interfaces;

public interface IEconomicActivitySourceClient
{
    Task<IReadOnlyList<SourceEconomicActivityDto>> GetActivitiesAsync(
        CancellationToken cancellationToken = default);
}