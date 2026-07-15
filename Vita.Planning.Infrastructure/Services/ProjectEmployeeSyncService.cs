using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Vita.Planning.Application.DTOs;
using Vita.Planning.Application.Interfaces;
using Vita.Planning.Infrastructure.Data;

namespace Vita.Planning.Infrastructure.Services;

public sealed class ProjectEmployeeSyncService : IProjectEmployeeSyncService
{
    private readonly PlanningDbContext _db;
    private readonly IEconomicProjectEmployeeSourceClient _sourceClient;
    private readonly ISyncRunService _syncRunService;

    public ProjectEmployeeSyncService(
        PlanningDbContext db,
        IEconomicProjectEmployeeSourceClient sourceClient,
        ISyncRunService syncRunService)
    {
        _db = db;
        _sourceClient = sourceClient;
        _syncRunService = syncRunService;
    }

    public async Task<ProjectEmployeeSyncResultDto> SyncProjectEmployeesAsync(
        string initiatedBy,
        CancellationToken cancellationToken = default)
    {
        var syncRunId = await _syncRunService.StartRunAsync(
            "economic_projects_api",
            "project_employees",
            initiatedBy,
            "Sync project employees from e-conomic",
            cancellationToken);

        var rowsRead = 0;
        var rowsInserted = 0;
        var rowsUpdated = 0;
        var errorCount = 0;

        var sourceItems = await _sourceClient.GetProjectEmployeesAsync(cancellationToken);
        rowsRead = sourceItems.Count;

        var keys = sourceItems.Select(x => x.Number).Distinct().ToList();

        var existing = await _db.ProjectEmployees
            .Where(x => keys.Contains(x.EmployeeNumber))
            .ToDictionaryAsync(x => x.EmployeeNumber, cancellationToken);

        foreach (var source in sourceItems)
        {
            if (existing.TryGetValue(source.Number, out var entity))
            {
                entity.Name = source.Name;
                entity.GroupNumber = source.GroupNumber;
                entity.CanApprove = source.CanApprove ?? false;
                entity.CanInvoice = source.CanInvoice ?? false;
                entity.IsBarred = source.IsBarred ?? false;
                entity.Address = source.Address;
                entity.City = source.City;
                entity.ZipCode = source.ZipCode;
                entity.CostPriceAfter = source.CostPriceAfter;
                entity.CostPriceBefore = source.CostPriceBefore;
                entity.SalesPriceAfter = source.SalesPriceAfter;
                entity.SalesPriceBefore = source.SalesPriceBefore;
                entity.CutoffDate = source.CutoffDate;
                entity.ObjectVersion = source.ObjectVersion;
                entity.SourceLastSyncedAt = DateTime.UtcNow;
                rowsUpdated++;
            }
            else
            {
                _db.ProjectEmployees.Add(new Vita.Planning.Infrastructure.Data.Entities.ExtProjectEmployee
                {
                    EmployeeNumber = source.Number,
                    Name = source.Name,
                    GroupNumber = source.GroupNumber,
                    CanApprove = source.CanApprove ?? false,
                    CanInvoice = source.CanInvoice ?? false,
                    IsBarred = source.IsBarred ?? false,
                    Address = source.Address,
                    City = source.City,
                    ZipCode = source.ZipCode,
                    CostPriceAfter = source.CostPriceAfter,
                    CostPriceBefore = source.CostPriceBefore,
                    SalesPriceAfter = source.SalesPriceAfter,
                    SalesPriceBefore = source.SalesPriceBefore,
                    CutoffDate = source.CutoffDate,
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
            notes: "Project employee sync completed",
            cancellationToken: cancellationToken);

        return new ProjectEmployeeSyncResultDto
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