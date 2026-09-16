using Vita.Atlas.Application.DTOs;

namespace Vita.Atlas.Application.Interfaces;

public interface ICompanyContactService
{
    Task<IReadOnlyList<CompanyContactDto>> GetByCustomerIdAsync(int customerId, CancellationToken cancellationToken = default);
    Task<CompanyContactDto> CreateAsync(int customerId, CreateCompanyContactRequest request, CancellationToken cancellationToken = default);
}
