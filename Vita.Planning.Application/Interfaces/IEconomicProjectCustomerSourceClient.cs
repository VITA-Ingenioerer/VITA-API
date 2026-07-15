using Vita.Planning.Application.DTOs;

namespace Vita.Planning.Application.Interfaces;

public interface IEconomicProjectCustomerSourceClient
{
    Task<IReadOnlyList<SourceEconomicProjectCustomerDto>> GetProjectCustomersAsync(
        CancellationToken cancellationToken = default);
}