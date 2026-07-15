using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Vita.Planning.Application.DTOs;
using Vita.Planning.Application.Interfaces;
using Vita.Planning.Infrastructure.Data;
using Vita.Planning.Infrastructure.Data.Entities;

namespace Vita.Planning.Infrastructure.Services;

public sealed class ProjectStatusSyncService : IProjectStatusSyncService
{
    private readonly PlanningDbContext _db;
    private readonly IEconomicProjectStatusSourceClient _sourceClient;
    private readonly ISyncRunService _syncRunService;

    public ProjectStatusSyncService(
        PlanningDbContext db,
        IEconomicProjectStatusSourceClient sourceClient,
        ISyncRunService syncRunService)
    {
        _db = db;
        _sourceClient = sourceClient;
        _syncRunService = syncRunService;
    }

    public async Task<ProjectStatusSyncResultDto> SyncProjectStatusesAsync(
        string initiatedBy,
        CancellationToken cancellationToken = default)
    {
        var syncRunId = await _syncRunService.StartRunAsync(
            "economic_projects_api",
            "project_statuses",
            initiatedBy,
            "Sync project statuses from e-conomic",
            cancellationToken);

        var rowsRead = 0;
        var rowsInserted = 0;
        var rowsUpdated = 0;
        var errorCount = 0;

        try
        {
            var sourceItems = await _sourceClient.GetProjectStatusesAsync(cancellationToken);
            rowsRead = sourceItems.Count;

            var keys = sourceItems.Select(x => x.Number).Distinct().ToList();

            var existing = await _db.ProjectStatuses
                .Where(x => keys.Contains(x.StatusNumber))
                .ToDictionaryAsync(x => x.StatusNumber, cancellationToken);

            foreach (var source in sourceItems)
            {
                try
                {
                    if (source.Number <= 0)
                    {
                        errorCount++;
                        await _syncRunService.LogErrorAsync(
                            syncRunId, "economic_projects_api", "project_statuses",
                            "validate", "Status number is required.",
                            source.Number.ToString(), rawPayload: JsonSerializer.Serialize(source),
                            cancellationToken: cancellationToken);
                        continue;
                    }

                    if (existing.TryGetValue(source.Number, out var entity))
                    {
                        entity.Name = source.Name;
                        entity.Priority = source.Priority;
                        entity.TypeNumber = source.TypeNumber;
                        entity.ObjectVersion = source.ObjectVersion;
                        entity.SourceLastSyncedAt = DateTime.UtcNow;
                        rowsUpdated++;
                    }
                    else
                    {
                        _db.ProjectStatuses.Add(new ExtProjectStatus
                        {
                            StatusNumber = source.Number,
                            Name = source.Name,
                            Priority = source.Priority,
                            TypeNumber = source.TypeNumber,
                            ObjectVersion = source.ObjectVersion,
                            SourceLastSyncedAt = DateTime.UtcNow
                        });
                        rowsInserted++;
                    }
                }
                catch (Exception ex)
                {
                    errorCount++;
                    await _syncRunService.LogErrorAsync(
                        syncRunId, "economic_projects_api", "project_statuses",
                        "map_upsert", ex.Message, source.Number.ToString(),
                        rawPayload: JsonSerializer.Serialize(source),
                        cancellationToken: cancellationToken);
                }
            }

            await _db.SaveChangesAsync(cancellationToken);

            var status = errorCount > 0 ? "partial" : "succeeded";

            await _syncRunService.CompleteRunAsync(
                syncRunId, status, rowsRead, rowsInserted, rowsUpdated,
                errorCount: errorCount,
                notes: "Project status sync completed",
                cancellationToken: cancellationToken);

            return new ProjectStatusSyncResultDto
            {
                SyncRunId = syncRunId,
                RowsRead = rowsRead,
                RowsInserted = rowsInserted,
                RowsUpdated = rowsUpdated,
                ErrorCount = errorCount,
                Status = status
            };
        }
        catch (DbUpdateException ex)
        {
            _db.ChangeTracker.Clear();
            errorCount++;

            await _syncRunService.LogErrorAsync(
                syncRunId, "economic_projects_api", "project_statuses",
                "save", ex.InnerException?.Message ?? ex.Message,
                cancellationToken: cancellationToken);

            await _syncRunService.CompleteRunAsync(
                syncRunId, "failed", rowsRead, rowsInserted, rowsUpdated,
                errorCount: errorCount,
                notes: "Project status sync failed during save",
                cancellationToken: cancellationToken);

            throw;
        }
    }
}