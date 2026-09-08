using Vita.Planning.Application.DTOs;

namespace Vita.Planning.Application.Interfaces;

public interface ISalesAnalyticsService
{
    Task<SalesAnalyticsDto> GetSalesAnalyticsAsync(CancellationToken cancellationToken = default);
}
