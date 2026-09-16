using Vita.Atlas.Application.DTOs;

namespace Vita.Atlas.Application.Interfaces;

public interface IEconomicProjectSourceClient
{
    Task<IReadOnlyList<SourceEconomicProjectDto>> GetProjectsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches a single project by number, or null if e-conomic doesn't have it (e.g. not found).
    /// </summary>
    Task<SourceEconomicProjectDto?> GetProjectByNumberAsync(int projectNumber, CancellationToken cancellationToken = default);
}