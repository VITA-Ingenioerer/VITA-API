using Microsoft.EntityFrameworkCore;
using Vita.Planning.Application.DTOs;
using Vita.Planning.Application.Interfaces;
using Vita.Planning.Infrastructure.Data;
using Vita.Planning.Infrastructure.Data.Entities;

namespace Vita.Planning.Infrastructure.Services;

public sealed class TimeEntrySyncService : ITimeEntrySyncService
{
    private const string SourceSystem = "economic_time_entries_api";
    private const string ResourceName = "time_entries";

    // A full backfill can be hundreds of thousands of rows; saving in batches means one
    // bad row (e.g. a source value that doesn't fit a column) only costs its own batch
    // instead of losing every row already processed in this run.
    private const int BatchSize = 500;

    private readonly PlanningDbContext _db;
    private readonly IEconomicTimeEntryClient _sourceClient;
    private readonly ISyncRunService _syncRunService;

    public TimeEntrySyncService(
        PlanningDbContext db,
        IEconomicTimeEntryClient sourceClient,
        ISyncRunService syncRunService)
    {
        _db = db;
        _sourceClient = sourceClient;
        _syncRunService = syncRunService;
    }

    public async Task<TimeEntrySyncResultDto> SyncAllTimeEntriesAsync(
        string initiatedBy, CancellationToken cancellationToken = default)
    {
        var syncRunId = await _syncRunService.StartRunAsync(
            SourceSystem, ResourceName, initiatedBy,
            "Full backfill of all time entries from e-conomic", cancellationToken);

        return await RunSyncAsync(syncRunId, updatedSinceUtc: null, cancellationToken);
    }

    public async Task<TimeEntrySyncResultDto> SyncNewTimeEntriesAsync(
        string initiatedBy, CancellationToken cancellationToken = default)
    {
        var watermark = await _db.TimeEntries
            .Select(x => x.LastUpdated)
            .MaxAsync(cancellationToken);

        var syncRunId = await _syncRunService.StartRunAsync(
            SourceSystem, ResourceName, initiatedBy,
            watermark.HasValue
                ? $"Incremental sync of time entries updated since {watermark:O}"
                : "No existing time entries found; running full backfill instead",
            cancellationToken);

        return await RunSyncAsync(syncRunId, watermark, cancellationToken);
    }

    private async Task<TimeEntrySyncResultDto> RunSyncAsync(
        long syncRunId, DateTime? updatedSinceUtc, CancellationToken cancellationToken)
    {
        var rowsInserted = 0;
        var rowsUpdated = 0;
        var errorCount = 0;
        var rowsRead = 0;

        try
        {
            var sourceItems = await _sourceClient.GetTimeEntriesUpdatedSinceAsync(updatedSinceUtc, cancellationToken);
            rowsRead = sourceItems.Count;

            var keys = sourceItems.Select(x => x.Number).Distinct().ToList();

            var existing = await _db.TimeEntries
                .Where(x => keys.Contains(x.Number))
                .ToDictionaryAsync(x => x.Number, cancellationToken);

            foreach (var batch in sourceItems.Chunk(BatchSize))
            {
                foreach (var source in batch)
                {
                    if (existing.TryGetValue(source.Number, out var entity))
                    {
                        ApplySource(entity, source);
                    }
                    else
                    {
                        entity = new ExtTimeEntry { Number = source.Number };
                        ApplySource(entity, source);
                        _db.TimeEntries.Add(entity);
                        existing[source.Number] = entity;
                    }
                }

                var pendingInserted = _db.ChangeTracker.Entries<ExtTimeEntry>().Count(e => e.State == EntityState.Added);
                var pendingUpdated = _db.ChangeTracker.Entries<ExtTimeEntry>().Count(e => e.State == EntityState.Modified);

                try
                {
                    await _db.SaveChangesAsync(cancellationToken);

                    rowsInserted += pendingInserted;
                    rowsUpdated += pendingUpdated;
                }
                catch (DbUpdateException ex)
                {
                    errorCount++;

                    await _syncRunService.LogErrorAsync(
                        syncRunId, SourceSystem, ResourceName, "batch-save",
                        ex.InnerException?.Message ?? ex.Message,
                        recordKey: batch[0].Number.ToString(),
                        cancellationToken: cancellationToken);

                    _db.ChangeTracker.Clear();
                }
            }

            var status = errorCount == 0
                ? "succeeded"
                : rowsInserted + rowsUpdated > 0 ? "partial" : "failed";

            await _syncRunService.CompleteRunAsync(
                syncRunId, status, rowsRead, rowsInserted, rowsUpdated,
                errorCount: errorCount,
                notes: "Time entry sync completed",
                cancellationToken: cancellationToken);

            return new TimeEntrySyncResultDto
            {
                SyncRunId = syncRunId,
                RowsRead = rowsRead,
                RowsInserted = rowsInserted,
                RowsUpdated = rowsUpdated,
                ErrorCount = errorCount,
                Status = status
            };
        }
        catch (Exception ex)
        {
            await _syncRunService.LogErrorAsync(
                syncRunId, SourceSystem, ResourceName, "sync", ex.Message,
                cancellationToken: cancellationToken);

            await _syncRunService.CompleteRunAsync(
                syncRunId, "failed", rowsRead: rowsRead, rowsInserted: rowsInserted, rowsUpdated: rowsUpdated,
                errorCount: errorCount + 1, notes: ex.Message, cancellationToken: cancellationToken);

            throw;
        }
    }

    private static void ApplySource(ExtTimeEntry entity, EconomicTimeEntryDto source)
    {
        entity.ProjectNumber = source.ProjectNumber;
        entity.ActivityNumber = source.ActivityNumber;
        entity.EmployeeNumber = source.EmployeeNumber;
        entity.Date = DateOnly.FromDateTime(source.Date);
        entity.Text = source.Text;
        entity.NumberOfHours = (decimal?)source.NumberOfHours;
        entity.IsApproved = source.IsApproved;
        entity.IsReconciled = source.IsReconciled;
        entity.LastUpdated = source.LastUpdated;
        entity.ObjectVersion = source.ObjectVersion;
        entity.SourceLastSyncedAt = DateTime.UtcNow;
    }
}
