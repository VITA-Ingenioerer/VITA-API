using Vita.Atlas.Application.DTOs;

namespace Vita.Atlas.Application.Interfaces;

public interface ICorrelationTraceService
{
    Task<CorrelationTraceDto> GetTraceAsync(Guid correlationId, CancellationToken cancellationToken = default);
}
