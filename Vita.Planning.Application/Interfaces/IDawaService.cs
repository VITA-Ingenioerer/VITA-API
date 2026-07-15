using Vita.Planning.Application.DTOs;

namespace Vita.Planning.Application.Interfaces;

public interface IDawaService
{
    Task<IReadOnlyList<DawaAddressSearchResultDto>> SearchAsync(string query, string? postalCode = null, string? regionCode = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DawaPostalCodeDto>> SearchPostalCodesAsync(string query, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DawaRegionDto>> GetRegionsAsync(CancellationToken cancellationToken = default);
}
