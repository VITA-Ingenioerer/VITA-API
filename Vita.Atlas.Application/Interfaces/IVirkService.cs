using Vita.Atlas.Application.DTOs;

namespace Vita.Atlas.Application.Interfaces;

public interface IVirkService
{
    Task<IReadOnlyList<VirkCompanySearchDto>> SearchCompaniesAsync(string query, CancellationToken cancellationToken = default);
    Task<VirkCompanyDto?> GetCompanyAsync(string cvrNumber, CancellationToken cancellationToken = default);
}
