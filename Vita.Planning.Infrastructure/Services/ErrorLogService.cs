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

    public async Task<PagedResultDto<OpsErrorDto>> QueryAsync(
        Guid? correlationId = null,
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize < 1 ? 50 : Math.Min(pageSize, 200);

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

        query = query.OrderByDescending(x => x.CreatedAtUtc).ThenByDescending(x => x.OpsErrorId);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => MapToDtoExpression(x))
            .ToListAsync(cancellationToken);

        return new PagedResultDto<OpsErrorDto>
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize),
            Items = items
        };
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
