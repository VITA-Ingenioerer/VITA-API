using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Vita.Planning.Application.DTOs;
using Vita.Planning.Application.Interfaces;
using Vita.Planning.Infrastructure.Data;
using Vita.Planning.Infrastructure.Data.Entities;

namespace Vita.Planning.Infrastructure.Services;

public sealed class ProjectGroupSyncService : IProjectGroupSyncService
{
    private readonly PlanningDbContext _db;
    private readonly IEconomicProjectGroupSourceClient _sourceClient;
    private readonly ISyncRunService _syncRunService;

    public ProjectGroupSyncService(
        PlanningDbContext db,
        IEconomicProjectGroupSourceClient sourceClient,
        ISyncRunService syncRunService)
    {
        _db = db;
        _sourceClient = sourceClient;
        _syncRunService = syncRunService;
    }

    public async Task<ProjectGroupSyncResultDto> SyncProjectGroupsAsync(string initiatedBy, CancellationToken cancellationToken = default)
    {
        var syncRunId = await _syncRunService.StartRunAsync(
            sourceSystem: "economic_projects_api",
            resourceName: "project_groups",
            initiatedBy: initiatedBy,
            notes: "Sync project groups from e-conomic",
            cancellationToken: cancellationToken);

        var rowsRead = 0;
        var rowsInserted = 0;
        var rowsUpdated = 0;
        var errorCount = 0;

        try
        {
            var sourceItems = await _sourceClient.GetProjectGroupsAsync(cancellationToken);
            rowsRead = sourceItems.Count;

            var keys = sourceItems.Select(x => x.Number).Distinct().ToList();

            var existing = await _db.ProjectGroups
                .Where(x => keys.Contains(x.ProjectGroupNumber))
                .ToDictionaryAsync(x => x.ProjectGroupNumber, cancellationToken);

            foreach (var source in sourceItems)
            {
                try
                {
                    if (source.Number <= 0 || string.IsNullOrWhiteSpace(source.Name))
                    {
                        errorCount++;

                        await _syncRunService.LogErrorAsync(
                            syncRunId,
                            "economic_projects_api",
                            "project_groups",
                            "validate",
                            "Project group number and name are required.",
                            source.Number.ToString(),
                            rawPayload: JsonSerializer.Serialize(source),
                            cancellationToken: cancellationToken);

                        continue;
                    }

                    if (existing.TryGetValue(source.Number, out var entity))
                    {
                        entity.Name = source.Name.Trim();
                        entity.TypeNumber = source.TypeNumber;
                        entity.CostAccountClosed = source.CostAccountClosed;
                        entity.CostAccountOngoing = source.CostAccountOngoing;
                        entity.CostAccountOngoingType = source.CostAccountOngoingType;
                        entity.CostContraAccountOngoing = source.CostContraAccountOngoing;
                        entity.SalesAccountClosed = source.SalesAccountClosed;
                        entity.SalesAccountOngoing = source.SalesAccountOngoing;
                        entity.SalesAccountOngoingType = source.SalesAccountOngoingType;
                        entity.SalesContraAccountOngoing = source.SalesContraAccountOngoing;
                        entity.IncludeCostPriceInFinance = source.IncludeCostPriceInFinance ?? false;
                        entity.IncludeSalesPriceInFinance = source.IncludeSalesPriceInFinance ?? false;
                        entity.ObjectVersion = source.ObjectVersion;
                        entity.SourceLastSyncedAt = DateTime.UtcNow;

                        rowsUpdated++;
                    }
                    else
                    {
                        _db.ProjectGroups.Add(new ExtProjectGroup
                        {
                            ProjectGroupNumber = source.Number,
                            Name = source.Name.Trim(),
                            TypeNumber = source.TypeNumber,
                            CostAccountClosed = source.CostAccountClosed,
                            CostAccountOngoing = source.CostAccountOngoing,
                            CostAccountOngoingType = source.CostAccountOngoingType,
                            CostContraAccountOngoing = source.CostContraAccountOngoing,
                            SalesAccountClosed = source.SalesAccountClosed,
                            SalesAccountOngoing = source.SalesAccountOngoing,
                            SalesAccountOngoingType = source.SalesAccountOngoingType,
                            SalesContraAccountOngoing = source.SalesContraAccountOngoing,
                            IncludeCostPriceInFinance = source.IncludeCostPriceInFinance ?? false,
                            IncludeSalesPriceInFinance = source.IncludeSalesPriceInFinance ?? false,
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
                        syncRunId,
                        "economic_projects_api",
                        "project_groups",
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
                notes: "Project group sync completed",
                cancellationToken: cancellationToken);

            return new ProjectGroupSyncResultDto
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
                "project_groups",
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
                notes: "Project group sync failed",
                cancellationToken: cancellationToken);

            throw;
        }
    }
}