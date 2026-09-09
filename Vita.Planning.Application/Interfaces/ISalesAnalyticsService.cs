using Vita.Planning.Application.DTOs;

namespace Vita.Planning.Application.Interfaces;

public interface ISalesAnalyticsService
{
    Task<SalesAnalyticsDto> GetSalesAnalyticsAsync(SalesAnalyticsFilterRequest? filter = null, CancellationToken cancellationToken = default);
}
