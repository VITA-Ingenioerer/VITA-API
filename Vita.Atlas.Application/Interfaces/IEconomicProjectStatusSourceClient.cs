using Vita.Atlas.Application.DTOs;

namespace Vita.Atlas.Application.Interfaces;

public interface IEconomicProjectStatusSourceClient
{
    Task<IReadOnlyList<SourceEconomicProjectStatusDto>> GetProjectStatusesAsync(
        CancellationToken cancellationToken = default);
}