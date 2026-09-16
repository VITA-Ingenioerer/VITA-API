using Vita.Atlas.Application.DTOs;

namespace Vita.Atlas.Application.Interfaces;

public interface IEconomicProjectActivitySourceClient
{
    Task<IReadOnlyList<SourceEconomicProjectActivityDto>> GetProjectActivitiesAsync(
        CancellationToken cancellationToken = default);
}
