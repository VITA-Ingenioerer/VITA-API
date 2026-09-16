using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Vita.Atlas.Application.DTOs;
using Vita.Atlas.Application.Interfaces;
using Vita.Atlas.Infrastructure.Data;
using Vita.Atlas.Infrastructure.Data.Entities;

namespace Vita.Atlas.Infrastructure.Services;

public sealed class ProjectSyncService : IProjectSyncService
{
    private readonly AtlasDbContext _db;
    private readonly IEconomicProjectSourceClient _sourceClient;
    private readonly ISyncRunService _syncRunService;

    public ProjectSyncService(
        AtlasDbContext db,
        IEconomicProjectSourceClient sourceClient,
        ISyncRunService syncRunService)
    {
        _db = db;
        _sourceClient = sourceClient;
        _syncRunService = syncRunService;
    }

    public async Task<ProjectSyncResultDto> SyncProjectsAsync(string initiatedBy, CancellationToken cancellationToken = default)
    {
        var syncRunId = await _syncRunService.StartRunAsync(
            "economic_projects_api",
            "projects",
            initiatedBy,
            "Sync projects from e-conomic Projects API",
            cancellationToken);

        var rowsRead = 0;
        var rowsInserted = 0;
        var rowsUpdated = 0;
        var errorCount = 0;

        try
        {
            var sourceProjects = await _sourceClient.GetProjectsAsync(cancellationToken);
            rowsRead = sourceProjects.Count;

            var projectNumbers = sourceProjects.Select(x => x.Number).Distinct().ToList();

            var existing = await _db.Projects
                .Where(x => projectNumbers.Contains(x.ProjectNumber))
                .ToDictionaryAsync(x => x.ProjectNumber, cancellationToken);

            foreach (var source in sourceProjects)
            {
                try
                {
                    if (source.Number <= 0 || string.IsNullOrWhiteSpace(source.Name))
                    {
                        errorCount++;
                        await _syncRunService.LogErrorAsync(
                            syncRunId,
                            "economic_projects_api",
                            "projects",
                            "validate",
                            "Project number and name are required.",
                            source.Number.ToString(),
                            rawPayload: JsonSerializer.Serialize(source),
                            cancellationToken: cancellationToken);
                        continue;
                    }

                    if (existing.TryGetValue(source.Number, out var entity))
                    {
                        ExtProjectMapper.ApplyFrom(entity, source);
                        rowsUpdated++;
                    }
                    else
                    {
                        _db.Projects.Add(ExtProjectMapper.ToNewEntity(source));
                        rowsInserted++;
                    }
                }
                catch (Exception ex)
                {
                    errorCount++;
                    await _syncRunService.LogErrorAsync(
                        syncRunId,
                        "economic_projects_api",
                        "projects",
                        "map_upsert",
                        ex.Message,
                        source.Number.ToString(),
                        rawPayload: JsonSerializer.Serialize(source),
                        cancellationToken: cancellationToken);
                }
            }

            await _db.SaveChangesAsync(cancellationToken);

            var status = errorCount > 0 ? "partial" : "succeeded";

            await _syncRunService.CompleteRunAsync(
                syncRunId,
                status,
                rowsRead,
                rowsInserted,
                rowsUpdated,
                errorCount: errorCount,
                notes: "Project sync completed",
                cancellationToken: cancellationToken);

            return new ProjectSyncResultDto
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
            errorCount++;
            await _syncRunService.LogErrorAsync(
                syncRunId,
                "economic_projects_api",
                "projects",
                "fetch",
                ex.Message,
                cancellationToken: cancellationToken);

            await _syncRunService.CompleteRunAsync(
                syncRunId,
                "failed",
                rowsRead,
                rowsInserted,
                rowsUpdated,
                errorCount: errorCount,
                notes: "Project sync failed",
                cancellationToken: cancellationToken);

            throw;
        }
    }
}