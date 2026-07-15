using Microsoft.EntityFrameworkCore;
using Vita.Planning.Application.DTOs;
using Vita.Planning.Application.Interfaces;
using Vita.Planning.Infrastructure.Data;
using Vita.Planning.Infrastructure.Data.Entities;

namespace Vita.Planning.Infrastructure.Services;

public sealed class ErrorLogService : IErrorLogService
{
    private const int MaxStackTraceLength = 4000;

    private readonly PlanningDbContext _dbContext;
    private readonly ICorrelationContext _correlationContext;

    public ErrorLogService(PlanningDbContext dbContext, ICorrelationContext correlationContext)
    {
        _dbContext = dbContext;
        _correlationContext = correlationContext;
    }

    public async Task<OpsErrorDto> LogAsync(RecordOpsErrorRequest request, CancellationToken cancellationToken = default)
    {
        var entity = new OpsError
        {
            ExceptionType = request.ExceptionType,
            Message = request.Message,
            StackTrace = Truncate(request.StackTrace, MaxStackTraceLength),
            RequestMethod = request.RequestMethod,
            RequestPath = request.RequestPath,
            StatusCode = request.StatusCode,
            CallerUserId = request.CallerUserId,
            CallerName = request.CallerName,
            CorrelationId = request.CorrelationId ?? _correlationContext.CorrelationId,
            CreatedAtUtc = DateTime.UtcNow
        };

        _dbContext.OpsErrors.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapToDto(entity);
    }

    public async Task<IReadOnlyList<OpsErrorDto>> QueryAsync(
        Guid? correlationId = null,
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        int take = 200,
        CancellationToken cancellationToken = default)
    {
        take = take < 1 ? 200 : Math.Min(take, 1000);

        var query = _dbContext.OpsErrors.AsNoTracking();

        if (correlationId.HasValue)
        {
            query = query.Where(x => x.CorrelationId == correlationId.Value);
        }

        if (fromUtc.HasValue)
        {
            query = query.Where(x => x.CreatedAtUtc >= fromUtc.Value);
        }

        if (toUtc.HasValue)
        {
            query = query.Where(x => x.CreatedAtUtc <= toUtc.Value);
        }

        return await query
            .OrderByDescending(x => x.CreatedAtUtc)
            .ThenByDescending(x => x.OpsErrorId)
            .Take(take)
            .Select(x => MapToDtoExpression(x))
            .ToListAsync(cancellationToken);
    }

    private static string? Truncate(string? value, int maxLength) =>
        string.IsNullOrEmpty(value) || value.Length <= maxLength ? value : value[..maxLength];

    private static OpsErrorDto MapToDtoExpression(OpsError x) => new()
    {
        OpsErrorId = x.OpsErrorId,
        ExceptionType = x.ExceptionType,
        Message = x.Message,
        StackTrace = x.StackTrace,
        RequestMethod = x.RequestMethod,
        RequestPath = x.RequestPath,
        StatusCode = x.StatusCode,
        CallerUserId = x.CallerUserId,
        CallerName = x.CallerName,
        CorrelationId = x.CorrelationId,
        CreatedAtUtc = x.CreatedAtUtc
    };

    private static OpsErrorDto MapToDto(OpsError x) => MapToDtoExpression(x);
}
