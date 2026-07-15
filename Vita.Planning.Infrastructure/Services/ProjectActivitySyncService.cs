using Microsoft.EntityFrameworkCore;
using Vita.Planning.Application.DTOs;
using Vita.Planning.Application.Interfaces;
using Vita.Planning.Infrastructure.Data;
using Vita.Planning.Infrastructure.Data.Entities;

namespace Vita.Planning.Infrastructure.Services;

public sealed class ProjectActivitySyncService : IProjectActivitySyncService
{
    private readonly PlanningDbContext _db;
    private readonly IEconomicProjectActivitySourceClient _sourceClient;
    private readonly ISyncRunService _syncRunService;

    public ProjectActivitySyncService(
        PlanningDbContext db,
        IEconomicProjectActivitySourceClient sourceClient,
        ISyncRunService syncRunService)
    {
        _db = db;
        _sourceClient = sourceClient;
        _syncRunService = syncRunService;
    }

    public async Task<ActivitySyncResultDto> SyncProjectActivitiesAsync(
        string initiatedBy,
        CancellationToken cancellationToken = default)
    {
        var syncRunId = await _syncRunService.StartRunAsync(
            "economic_projects_api",
            "project_activities",
            initiatedBy,
            "Sync project activities from e-conomic",
            cancellationToken);

        var rowsRead = 0;
        var rowsInserted = 0;
        var rowsUpdated = 0;
        var errorCount = 0;

        var sourceItems = await _sourceClient.GetProjectActivitiesAsync(cancellationToken);
        rowsRead = sourceItems.Count;

        var numbers = sourceItems.Select(x => x.Number).Distinct().ToList();

        var existing = await _db.ProjectActivities
            .Where(x => numbers.Contains(x.Number))
            .ToDictionaryAsync(x => x.Number, cancellationToken);

        var now = DateTime.UtcNow;

        foreach (var source in sourceItems)
        {
            if (existing.TryGetValue(source.Number, out var entity))
            {
                entity.ProjectNumber = source.ProjectNumber;
                entity.ActivityNumber = source.ActivityNumber;
                entity.StartDate = source.StartDate;
                entity.EndDate = source.EndDate;
                entity.ResponsibleEmployeeNumber = source.ResponsibleEmployeeNumber;
                entity.Completed = source.Completed ?? false;
                entity.ObjectVersion = source.ObjectVersion;
                entity.LastUpdatedUtc = source.LastUpdated;
                entity.SourceLastSyncedAt = now;
                rowsUpdated++;
            }
            else
            {
                _db.ProjectActivities.Add(new ExtProjectActivity
                {
                    Number = source.Number,
                    ProjectNumber = source.ProjectNumber,
                    ActivityNumber = source.ActivityNumber,
                    StartDate = source.StartDate,
                    EndDate = source.EndDate,
                    ResponsibleEmployeeNumber = source.ResponsibleEmployeeNumber,
                    Completed = source.Completed ?? false,
                    ObjectVersion = source.ObjectVersion,
                    LastUpdatedUtc = source.LastUpdated,
                    SourceLastSyncedAt = now
                });
                rowsInserted++;
            }
        }

        await _db.SaveChangesAsync(cancellationToken);

        await _syncRunService.CompleteRunAsync(
            syncRunId, "succeeded", rowsRead, rowsInserted, rowsUpdated,
            errorCount: errorCount,
            notes: "Project activity sync completed",
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
