using Vita.Planning.Application.DTOs;

namespace Vita.Planning.Application.Interfaces;

public interface ICustomerService
{
    Task<IReadOnlyList<CustomerDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CustomerDto>> SearchAsync(string query, CancellationToken cancellationToken = default);
    Task<CustomerDto?> GetByIdAsync(int customerId, CancellationToken cancellationToken = default);
    Task<CustomerDto> CreateAsync(CreateCustomerRequest request, CancellationToken cancellationToken = default);
    Task<CustomerDto?> UpdateAsync(int customerId, UpdateCustomerRequest request, CancellationToken cancellationToken = default);
    Task<CustomerDto?> LinkEconomicAsync(int customerId, LinkEconomicCustomerRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(int customerId, CancellationToken cancellationToken = default);
    Task<CustomerDto> CreateFromCvrAsync(CreateCustomerFromCvrRequest request, IVirkService virkService, CancellationToken cancellationToken = default);
}
