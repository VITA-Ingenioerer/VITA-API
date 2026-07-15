using Vita.Planning.Application.Interfaces;

namespace Vita.Planning.Api.Middleware;

/// <summary>
/// Reads X-Correlation-Id from the incoming request if present and valid,
/// otherwise generates one. Resolved lazily so it's computed at most once
/// per request regardless of how many services ask for it.
/// </summary>
public sealed class HttpCorrelationContext : ICorrelationContext
{
    private readonly Lazy<Guid> _correlationId;

    public HttpCorrelationContext(IHttpContextAccessor httpContextAccessor)
    {
        _correlationId = new Lazy<Guid>(() =>
        {
            var header = httpContextAccessor.HttpContext?.Request.Headers["X-Correlation-Id"].FirstOrDefault();
            return !string.IsNullOrWhiteSpace(header) && Guid.TryParse(header, out var parsed)
                ? parsed
                : Guid.NewGuid();
        });
    }

    public Guid CorrelationId => _correlationId.Value;
}
