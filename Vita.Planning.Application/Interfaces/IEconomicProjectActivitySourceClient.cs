using Vita.Planning.Application.DTOs;

namespace Vita.Planning.Application.Interfaces;

public interface IEconomicProjectActivitySourceClient
{
    Task<IReadOnlyList<SourceEconomicProjectActivityDto>> GetProjectActivitiesAsync(
        CancellationToken cancellationToken = default);
}
