using Vita.Planning.Application.DTOs;

namespace Vita.Planning.Application.Interfaces;

public interface IVirkService
{
    Task<IReadOnlyList<VirkCompanySearchDto>> SearchCompaniesAsync(string query, CancellationToken cancellationToken = default);
    Task<VirkCompanyDto?> GetCompanyAsync(string cvrNumber, CancellationToken cancellationToken = default);
}
