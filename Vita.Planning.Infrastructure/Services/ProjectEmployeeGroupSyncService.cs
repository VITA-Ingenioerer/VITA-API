using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Vita.Planning.Application.DTOs;
using Vita.Planning.Application.Interfaces;
using Vita.Planning.Infrastructure.Data;
using Vita.Planning.Infrastructure.Data.Entities;

namespace Vita.Planning.Infrastructure.Services;

public sealed class ProjectEmployeeGroupSyncService : IProjectEmployeeGroupSyncService
{
    private readonly PlanningDbContext _db;
    private readonly IEconomicProjectEmployeeGroupSourceClient _sourceClient;
    private readonly ISyncRunService _syncRunService;

    public ProjectEmployeeGroupSyncService(
        PlanningDbContext db,
        IEconomicProjectEmployeeGroupSourceClient sourceClient,
        ISyncRunService syncRunService)
    {
        _db = db;
        _sourceClient = sourceClient;
        _syncRunService = syncRunService;
    }

    public async Task<ProjectEmployeeGroupSyncResultDto> SyncProjectEmployeeGroupsAsync(
        string initiatedBy,
        CancellationToken cancellationToken = default)
    {
        var syncRunId = await _syncRunService.StartRunAsync(
            "economic_projects_api",
            "project_employee_groups",
            initiatedBy,
            "Sync project employee groups from e-conomic",
            cancellationToken);

        var rowsRead = 0;
        var rowsInserted = 0;
        var rowsUpdated = 0;
        var errorCount = 0;

        var sourceItems = await _sourceClient.GetProjectEmployeeGroupsAsync(cancellationToken);
        rowsRead = sourceItems.Count;

        var keys = sourceItems.Select(x => x.Number).Distinct().ToList();

        var existing = await _db.ProjectEmployeeGroups
            .Where(x => keys.Contains(x.EmployeeGroupNumber))
            .ToDictionaryAsync(x => x.EmployeeGroupNumber, cancellationToken);

        foreach (var source in sourceItems)
        {
            if (existing.TryGetValue(source.Number, out var entity))
            {
                entity.Name = source.Name;
                entity.ObjectVersion = source.ObjectVersion;
                entity.SourceLastSyncedAt = DateTime.UtcNow;
                rowsUpdated++;
            }
            else
            {
                _db.ProjectEmployeeGroups.Add(new ExtProjectEmployeeGroup
                {
                    EmployeeGroupNumber = source.Number,
                    Name = source.Name,
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
            notes: "Project employee group sync completed",
            cancellationToken: cancellationToken);

        return new ProjectEmployeeGroupSyncResultDto
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