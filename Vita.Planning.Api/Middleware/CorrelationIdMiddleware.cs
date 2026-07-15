using Vita.Planning.Application.Interfaces;

namespace Vita.Planning.Api.Middleware;

/// <summary>
/// Echoes the request's resolved correlation id back as a response header so
/// a caller can quote it when asking an engineer to look up what happened.
/// </summary>
public sealed class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ICorrelationContext correlationContext)
    {
        context.Response.OnStarting(() =>
        {
            context.Response.Headers["X-Correlation-Id"] = correlationContext.CorrelationId.ToString();
            return Task.CompletedTask;
        });

        await _next(context);
    }
}
