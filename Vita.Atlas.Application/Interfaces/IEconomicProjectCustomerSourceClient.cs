using Vita.Atlas.Application.DTOs;

namespace Vita.Atlas.Application.Interfaces;

public interface IEconomicProjectCustomerSourceClient
{
    Task<IReadOnlyList<SourceEconomicProjectCustomerDto>> GetProjectCustomersAsync(
        CancellationToken cancellationToken = default);
}