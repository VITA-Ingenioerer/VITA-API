using Vita.Planning.Application.DTOs;

namespace Vita.Planning.Application.Interfaces;

public interface IEconomicProjectEmployeeGroupSourceClient
{
    Task<IReadOnlyList<SourceEconomicProjectEmployeeGroupDto>> GetProjectEmployeeGroupsAsync(
        CancellationToken cancellationToken = default);
}