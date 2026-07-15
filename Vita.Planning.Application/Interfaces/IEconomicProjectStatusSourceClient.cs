using Vita.Planning.Application.DTOs;

namespace Vita.Planning.Application.Interfaces;

public interface IEconomicProjectStatusSourceClient
{
    Task<IReadOnlyList<SourceEconomicProjectStatusDto>> GetProjectStatusesAsync(
        CancellationToken cancellationToken = default);
}