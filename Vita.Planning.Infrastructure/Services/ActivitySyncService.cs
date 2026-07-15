using Microsoft.EntityFrameworkCore;
using Vita.Planning.Application.DTOs;
using Vita.Planning.Application.Interfaces;
using Vita.Planning.Infrastructure.Data;
using Vita.Planning.Infrastructure.Data.Entities;

namespace Vita.Planning.Infrastructure.Services;

public sealed class ActivitySyncService : IActivitySyncService
{
    private readonly PlanningDbContext _db;
    private readonly IEconomicActivitySourceClient _sourceClient;
    private readonly ISyncRunService _syncRunService;

    public ActivitySyncService(
        PlanningDbContext db,
        IEconomicActivitySourceClient sourceClient,
        ISyncRunService syncRunService)
    {
        _db = db;
        _sourceClient = sourceClient;
        _syncRunService = syncRunService;
    }

    public async Task<ActivitySyncResultDto> SyncActivitiesAsync(
        string initiatedBy,
        CancellationToken cancellationToken = default)
    {
        var syncRunId = await _syncRunService.StartRunAsync(
            "economic_projects_api",
            "activities",
            initiatedBy,
            "Sync activities from e-conomic",
            cancellationToken);

        var rowsRead = 0;
        var rowsInserted = 0;
        var rowsUpdated = 0;
        var errorCount = 0;

        var sourceItems = await _sourceClient.GetActivitiesAsync(cancellationToken);
        rowsRead = sourceItems.Count;

        var keys = sourceItems.Select(x => x.Number).Distinct().ToList();

        var existing = await _db.Activities
            .Where(x => keys.Contains(x.ActivityNumber))
            .ToDictionaryAsync(x => x.ActivityNumber, cancellationToken);

        foreach (var source in sourceItems)
        {
            if (existing.TryGetValue(source.Number, out var entity))
            {
                entity.ActivityGroupNumber = source.ActivityGroupNumber;
                entity.Name = source.Name;
                entity.CostPriceMarkupPercentage = source.CostPriceMarkupPercentage;
                entity.CutoffDate = source.CutoffDate;
                entity.HideInSearch = source.HideInSearch;
                entity.InLieuCode = source.InLieuCode;
                entity.IsAccessible = source.IsAccessible;
                entity.SalesPriceAfter = source.SalesPriceAfter;
                entity.SalesPriceBefore = source.SalesPriceBefore;
                entity.ObjectVersion = source.ObjectVersion;
                entity.SourceLastSyncedAt = DateTime.UtcNow;
                rowsUpdated++;
            }
            else
            {
                _db.Activities.Add(new ExtActivity
                {
                    ActivityNumber = source.Number,
                    ActivityGroupNumber = source.ActivityGroupNumber,
                    Name = source.Name,
                    CostPriceMarkupPercentage = source.CostPriceMarkupPercentage,
                    CutoffDate = source.CutoffDate,
                    HideInSearch = source.HideInSearch,
                    InLieuCode = source.InLieuCode,
                    IsAccessible = source.IsAccessible,
                    SalesPriceAfter = source.SalesPriceAfter,
                    SalesPriceBefore = source.SalesPriceBefore,
                    ObjectVersion = source.ObjectVersion,
                    SourceLastSyncedAt = DateTime.UtcNow
                });
                rowsInserted++;
            }
        }

        await _db.SaveChangesAsync(cancellationToken);

        await _syncRunService.CompleteRunAsync(
            syncRunId, "succeeded", rowsRead, rowsInserted, rowsUpdated,
            errorCount: errorCount,
            notes: "Activity sync completed",
            cancellationToken: cancellationToken);

        return new ActivitySyncResultDto
        {
            SyncRunId = syncRunId,
            RowsRead = rowsRead,
            RowsInserted = rowsInserted,
            RowsUpdated = rowsUpdated,
            ErrorCount = errorCount,
            Status = "succeeded"
        };
    }
}