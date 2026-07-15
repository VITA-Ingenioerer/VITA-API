using Microsoft.EntityFrameworkCore;
using Vita.Planning.Application.DTOs;
using Vita.Planning.Application.Interfaces;
using Vita.Planning.Infrastructure.Data;
using Vita.Planning.Infrastructure.Data.Entities;

namespace Vita.Planning.Infrastructure.Services;

public sealed class SyncRunService : ISyncRunService
{
    private readonly PlanningDbContext _db;
    private readonly ICorrelationContext _correlationContext;

    public SyncRunService(PlanningDbContext db, ICorrelationContext correlationContext)
    {
        _db = db;
        _correlationContext = correlationContext;
    }

    public async Task<PagedResultDto<SyncRunDto>> QueryRunsAsync(
        string? sourceSystem = null,
        string? status = null,
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize < 1 ? 50 : Math.Min(pageSize, 200);

        var query = _db.SyncRuns.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(sourceSystem))
        {
            query = query.Where(x => x.SourceSystem == sourceSystem.Trim());
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(x => x.Status == status.Trim());
        }

        if (fromUtc.HasValue)
        {
            query = query.Where(x => x.StartedAtUtc >= fromUtc.Value);
        }

        if (toUtc.HasValue)
        {
            query = query.Where(x => x.StartedAtUtc <= toUtc.Value);
        }

        query = query.OrderByDescending(x => x.StartedAtUtc).ThenByDescending(x => x.SyncRunId);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => MapToDtoExpression(x))
            .ToListAsync(cancellationToken);

        return new PagedResultDto<SyncRunDto>
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize),
            Items = items
        };
    }

    public async Task<IReadOnlyList<SyncRunDto>> GetByCorrelationIdAsync(
        Guid correlationId,
        CancellationToken cancellationToken = default)
    {
        return await _db.SyncRuns
            .AsNoTracking()
            .Where(x => x.CorrelationId == correlationId)
            .OrderBy(x => x.StartedAtUtc)
            .Select(x => MapToDtoExpression(x))
            .ToListAsync(cancellationToken);
    }

    private static SyncRunDto MapToDtoExpression(OpsSyncRun x) => new()
    {
        SyncRunId = x.SyncRunId,
        SourceSystem = x.SourceSystem,
        ResourceName = x.ResourceName,
        StartedAtUtc = x.StartedAtUtc,
        CompletedAtUtc = x.CompletedAtUtc,
        Status = x.Status,
        RowsRead = x.RowsRead,
        RowsInserted = x.RowsInserted,
        RowsUpdated = x.RowsUpdated,
        RowsDeleted = x.RowsDeleted,
        ErrorCount = x.ErrorCount,
        InitiatedBy = x.InitiatedBy,
        Notes = x.Notes,
        CorrelationId = x.CorrelationId
    };

    public async Task<IReadOnlyList<SyncErrorDto>> GetErrorsByRunIdAsync(
        long syncRunId,
        CancellationToken cancellationToken = default)
    {
        return await _db.SyncErrors
            .AsNoTracking()
            .Where(x => x.SyncRunId == syncRunId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => new SyncErrorDto
            {
                SyncErrorId = x.SyncErrorId,
                SyncRunId = x.SyncRunId,
                SourceSystem = x.SourceSystem,
                ResourceName = x.ResourceName,
                RecordKey = x.RecordKey,
                ErrorStage = x.ErrorStage,
                ErrorCode = x.ErrorCode,
                ErrorMessage = x.ErrorMessage,
                RawPayload = x.RawPayload,
                CreatedAtUtc = x.CreatedAtUtc
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<long> StartRunAsync(
        string sourceSystem,
        string resourceName,
        string initiatedBy,
        string? notes = null,
        CancellationToken cancellationToken = default)
    {
        var run = new OpsSyncRun
        {
            SourceSystem = sourceSystem,
            ResourceName = resourceName,
            StartedAtUtc = DateTime.UtcNow,
            Status = "running",
            InitiatedBy = initiatedBy,
            Notes = notes,
            ErrorCount = 0,
            CorrelationId = _correlationContext.CorrelationId
        };

        _db.SyncRuns.Add(run);
        await _db.SaveChangesAsync(cancellationToken);

        return run.SyncRunId;
    }

    public async Task CompleteRunAsync(
        long syncRunId,
        string status,
        int? rowsRead = null,
        int? rowsInserted = null,
        int? rowsUpdated = null,
        int? rowsDeleted = null,
        int errorCount = 0,
        string? notes = null,
        CancellationToken cancellationToken = default)
    {
        var run = await _db.SyncRuns.FirstOrDefaultAsync(x => x.SyncRunId == syncRunId, cancellationToken);

        if (run is null)
            throw new InvalidOperationException($"Sync run {syncRunId} was not found.");

        run.CompletedAtUtc = DateTime.UtcNow;
        run.Status = status;
        run.RowsRead = rowsRead;
        run.RowsInserted = rowsInserted;
        run.RowsUpdated = rowsUpdated;
        run.RowsDeleted = rowsDeleted;
        run.ErrorCount = errorCount;
        run.Notes = notes ?? run.Notes;

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task LogErrorAsync(
        long syncRunId,
        string sourceSystem,
        string resourceName,
        string errorStage,
        string errorMessage,
        string? recordKey = null,
        string? errorCode = null,
        string? rawPayload = null,
        CancellationToken cancellationToken = default)
    {
        var error = new OpsSyncError
        {
            SyncRunId = syncRunId,
            SourceSystem = sourceSystem,
            ResourceName = resourceName,
            RecordKey = recordKey,
            ErrorStage = errorStage,
            ErrorCode = errorCode,
            ErrorMessage = errorMessage,
            RawPayload = rawPayload,
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.SyncErrors.Add(error);
        await _db.SaveChangesAsync(cancellationToken);
    }
}