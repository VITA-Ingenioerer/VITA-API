using Vita.Atlas.Application.DTOs;

namespace Vita.Atlas.Application.Interfaces;

public interface ISalesAnalyticsService
{
    Task<SalesAnalyticsDto> GetSalesAnalyticsAsync(SalesAnalyticsFilterRequest? filter = null, CancellationToken cancellationToken = default);
}
