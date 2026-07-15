using Microsoft.AspNetCore.Mvc;
using Vita.Planning.Application.DTOs;
using Vita.Planning.Application.Interfaces;

namespace Vita.Planning.Api.Middleware;

/// <summary>
/// Catches anything that escapes controller-level try/catch, persists it to
/// core.ops_errors (previously these vanished into a bare 500 with no trace),
/// and returns a ProblemDetails body carrying the correlation id so a caller
/// can quote it back when reporting an issue.
/// </summary>
public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IErrorLogService errorLogService, ICorrelationContext correlationContext)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            var caller = context.User.Identity?.IsAuthenticated == true
                ? CallerInfo.FromClaimsPrincipal(context.User)
                : null;

            try
            {
                await errorLogService.LogAsync(new RecordOpsErrorRequest
                {
                    ExceptionType = ex.GetType().FullName ?? ex.GetType().Name,
                    Message = ex.Message,
                    StackTrace = ex.ToString(),
                    RequestMethod = context.Request.Method,
                    RequestPath = context.Request.Path,
                    StatusCode = StatusCodes.Status500InternalServerError,
                    CallerUserId = caller?.UserId,
                    CallerName = caller?.Name,
                    CorrelationId = correlationContext.CorrelationId
                });
            }
            catch (Exception logEx)
            {
                // Never let a broken error-logging path turn a normal 500 into an unhandled crash.
                _logger.LogError(logEx, "Failed to persist error log entry for correlation id {CorrelationId}", correlationContext.CorrelationId);
            }

            if (context.Response.HasStarted)
            {
                throw;
            }

            context.Response.Clear();
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/problem+json";

            await context.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "An unexpected error occurred.",
                Detail = $"Correlation Id: {correlationContext.CorrelationId}",
                Instance = context.Request.Path
            });
        }
    }
}
