using Vita.Planning.Application.DTOs;

namespace Vita.Planning.Application.Interfaces;

public interface ICorrelationTraceService
{
    Task<CorrelationTraceDto> GetTraceAsync(Guid correlationId, CancellationToken cancellationToken = default);
}
