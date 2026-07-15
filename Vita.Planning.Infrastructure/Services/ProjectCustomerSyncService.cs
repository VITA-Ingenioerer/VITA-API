using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Vita.Planning.Application.DTOs;
using Vita.Planning.Application.Interfaces;
using Vita.Planning.Infrastructure.Data;
using Vita.Planning.Infrastructure.Data.Entities;

namespace Vita.Planning.Infrastructure.Services;

public sealed class ProjectCustomerSyncService : IProjectCustomerSyncService
{
    private readonly PlanningDbContext _db;
    private readonly IEconomicProjectCustomerSourceClient _sourceClient;
    private readonly ISyncRunService _syncRunService;

    public ProjectCustomerSyncService(
        PlanningDbContext db,
        IEconomicProjectCustomerSourceClient sourceClient,
        ISyncRunService syncRunService)
    {
        _db = db;
        _sourceClient = sourceClient;
        _syncRunService = syncRunService;
    }

    public async Task<ProjectCustomerSyncResultDto> SyncProjectCustomersAsync(
        string initiatedBy,
        CancellationToken cancellationToken = default)
    {
        var syncRunId = await _syncRunService.StartRunAsync(
            sourceSystem: "economic_projects_api",
            resourceName: "project_customers",
            initiatedBy: initiatedBy,
            notes: "Sync project customers from e-conomic",
            cancellationToken: cancellationToken);

        var rowsRead = 0;
        var rowsInserted = 0;
        var rowsUpdated = 0;
        var errorCount = 0;

        try
        {
            var sourceItems = await _sourceClient.GetProjectCustomersAsync(cancellationToken);
            rowsRead = sourceItems.Count;

            var keys = sourceItems
                .Select(x => x.Number)
                .Distinct()
                .ToList();

            var existing = await _db.ProjectCustomers
                .Where(x => keys.Contains(x.CustomerNumber))
                .ToDictionaryAsync(x => x.CustomerNumber, cancellationToken);

            var existingCoreCustomers = await _db.Customers
                .Where(x => x.ExtCustomerNumber != null && keys.Contains(x.ExtCustomerNumber.Value))
                .ToDictionaryAsync(x => x.ExtCustomerNumber!.Value, cancellationToken);

            var cvrNumbers = sourceItems
                .Select(x => x.CorporateIdentificationNumber)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x!)
                .Distinct()
                .ToList();

            var existingCoreCustomersByCvr = await _db.Customers
                .Where(x => x.ExtCustomerNumber == null && x.CvrNumber != null && cvrNumbers.Contains(x.CvrNumber))
                .ToDictionaryAsync(x => x.CvrNumber!, cancellationToken);

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
                            "project_customers",
                            "validate",
                            "Customer number and name are required.",
                            source.Number.ToString(),
                            rawPayload: JsonSerializer.Serialize(source),
                            cancellationToken: cancellationToken);

                        continue;
                    }

                    // Upsert ext.project_customers (read-only sync mirror)
                    if (existing.TryGetValue(source.Number, out var entity))
                    {
                        entity.Name = source.Name.Trim();
                        entity.ObjectVersion = source.ObjectVersion;
                        entity.SourceLastSyncedAt = DateTime.UtcNow;

                        rowsUpdated++;
                    }
                    else
                    {
                        _db.ProjectCustomers.Add(new ExtProjectCustomer
                        {
                            CustomerNumber = source.Number,
                            Name = source.Name.Trim(),
                            ObjectVersion = source.ObjectVersion,
                            SourceLastSyncedAt = DateTime.UtcNow
                        });

                        rowsInserted++;
                    }

                    // Upsert core.customers so the frontend sees all customers in one place
                    var cvr = string.IsNullOrWhiteSpace(source.CorporateIdentificationNumber)
                        ? null
                        : source.CorporateIdentificationNumber.Trim();

                    if (existingCoreCustomers.TryGetValue(source.Number, out var coreCustomer))
                    {
                        coreCustomer.Name = source.Name.Trim();
                        coreCustomer.CustomerSource = "Economic";
                        coreCustomer.ObjectVersion = source.ObjectVersion;
                        coreCustomer.SourceLastSyncedAt = DateTime.UtcNow;
                        coreCustomer.UpdatedAtUtc = DateTime.UtcNow;
                    }
                    else if (cvr != null && existingCoreCustomersByCvr.TryGetValue(cvr, out var cvrMatch))
                    {
                        // Merge: local customer with same CVR — link it to e-conomic
                        cvrMatch.ExtCustomerNumber = source.Number;
                        cvrMatch.Name = source.Name.Trim();
                        cvrMatch.CustomerSource = "Economic";
                        cvrMatch.ObjectVersion = source.ObjectVersion;
                        cvrMatch.SourceLastSyncedAt = DateTime.UtcNow;
                        cvrMatch.UpdatedAtUtc = DateTime.UtcNow;

                        // Make it visible to the ExtCustomerNumber lookup on future syncs
                        existingCoreCustomers[source.Number] = cvrMatch;
                    }
                    else
                    {
                        _db.Customers.Add(new Customer
                        {
                            ExtCustomerNumber = source.Number,
                            Name = source.Name.Trim(),
                            CustomerSource = "Economic",
                            CustomerStatus = "Active",
                            CvrNumber = cvr,
                            ObjectVersion = source.ObjectVersion,
                            SourceLastSyncedAt = DateTime.UtcNow,
                            CountryCode = "DK",
                            CreatedAtUtc = DateTime.UtcNow,
                        });
                    }
                }
                catch (Exception ex)
                {
                    errorCount++;

                    await _syncRunService.LogErrorAsync(
                        syncRunId,
                        "economic_projects_api",
                        "project_customers",
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
                notes: "Project customer sync completed",
                cancellationToken: cancellationToken);

            return new ProjectCustomerSyncResultDto
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
                syncRunId,
                "economic_projects_api",
                "project_customers",
                "save",
                ex.InnerException?.Message ?? ex.Message,
                cancellationToken: cancellationToken);

            await _syncRunService.CompleteRunAsync(
                syncRunId,
                "failed",
                rowsRead,
                rowsInserted,
                rowsUpdated,
                errorCount: errorCount,
                notes: "Project customer sync failed during database save",
                cancellationToken: cancellationToken);

            throw;
        }
        catch (Exception ex)
        {
            _db.ChangeTracker.Clear();

            errorCount++;

            await _syncRunService.LogErrorAsync(
                syncRunId,
                "economic_projects_api",
                "project_customers",
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
                notes: "Project customer sync failed",
                cancellationToken: cancellationToken);

            throw;
        }
    }
}