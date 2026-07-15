using Microsoft.EntityFrameworkCore;
using Vita.Planning.Application.DTOs;
using Vita.Planning.Application.Interfaces;
using Vita.Planning.Infrastructure.Data;
using Vita.Planning.Infrastructure.Data.Entities;

namespace Vita.Planning.Infrastructure.Services;

public sealed class UserSyncService : IUserSyncService
{
    private readonly PlanningDbContext _db;
    private readonly IInternalUserSourceClient _sourceClient;
    private readonly ISyncRunService _syncRunService;
    private readonly IEntityChangeLogService _changeLogService;

    public UserSyncService(
        PlanningDbContext db,
        IInternalUserSourceClient sourceClient,
        ISyncRunService syncRunService,
        IEntityChangeLogService changeLogService)
    {
        _db = db;
        _sourceClient = sourceClient;
        _syncRunService = syncRunService;
        _changeLogService = changeLogService;
    }

    private static CallerInfo ResolveCaller(string? initiatedBy) =>
        string.IsNullOrWhiteSpace(initiatedBy)
            ? CallerInfo.System
            : new CallerInfo { UserId = initiatedBy, Name = initiatedBy, Email = string.Empty };

    private static object BuildUserSnapshot(ExtUser user) => new
    {
        user.UserPrincipalName,
        user.DisplayName,
        user.OfficeLocation,
        user.Department,
        user.EmployeeType,
        user.ManagerEmployeeId,
        user.IsActive
    };

    public async Task<UserSyncResultDto> SyncUsersAsync(string initiatedBy, CancellationToken cancellationToken = default)
    {
        var syncRunId = await _syncRunService.StartRunAsync(
            sourceSystem: "economic_graph_users",
            resourceName: "users",
            initiatedBy: initiatedBy,
            notes: "Sync users from Economic project employees merged with Microsoft Graph",
            cancellationToken: cancellationToken);

        var rowsRead = 0;
        var rowsInserted = 0;
        var rowsUpdated = 0;
        var errorCount = 0;

        try
        {
            var sourceUsers = await _sourceClient.GetUsersAsync(cancellationToken);
            rowsRead = sourceUsers.Count;

            var sourceEmployeeIds = sourceUsers
                .Where(x => x.EmployeeId > 0)
                .Select(x => x.EmployeeId)
                .ToHashSet();

            var managerUserPrincipalNames = sourceUsers
                .Select(x => x.ManagerUserPrincipalName)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x!.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var managerDisplayNames = sourceUsers
                .Select(GetManagerDisplayName)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x!.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var existingUsers = await _db.Users.ToListAsync(cancellationToken);

            var existingUsersByEmployeeId = existingUsers
                .ToDictionary(x => x.EmployeeId);

            var existingUsersByUserPrincipalName = existingUsers
                .Where(x => !string.IsNullOrWhiteSpace(x.UserPrincipalName))
                .GroupBy(x => x.UserPrincipalName.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    x => x.Key,
                    x => x.OrderBy(y => y.EmployeeId).First(),
                    StringComparer.OrdinalIgnoreCase);

            var existingManagersByUserPrincipalName = existingUsers
                .Where(x => !string.IsNullOrWhiteSpace(x.UserPrincipalName)
                    && managerUserPrincipalNames.Contains(x.UserPrincipalName.Trim(), StringComparer.OrdinalIgnoreCase))
                .GroupBy(x => x.UserPrincipalName.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    x => x.Key,
                    x => x.OrderBy(y => y.EmployeeId).Select(y => y.EmployeeId).First(),
                    StringComparer.OrdinalIgnoreCase);

            var existingManagersByDisplayName = existingUsers
                .Where(x => !string.IsNullOrWhiteSpace(x.DisplayName)
                    && managerDisplayNames.Contains(x.DisplayName.Trim(), StringComparer.OrdinalIgnoreCase))
                .GroupBy(x => x.DisplayName.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    x => x.Key,
                    x => x.OrderBy(y => y.EmployeeId).Select(y => y.EmployeeId).First(),
                    StringComparer.OrdinalIgnoreCase);

            var sourceManagersByUserPrincipalName = sourceUsers
                .Where(x => x.EmployeeId > 0 && !string.IsNullOrWhiteSpace(x.UserPrincipalName))
                .GroupBy(x => x.UserPrincipalName.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    x => x.Key,
                    x => x.OrderBy(y => y.EmployeeId).Select(y => y.EmployeeId).First(),
                    StringComparer.OrdinalIgnoreCase);

            var sourceManagersByDisplayName = sourceUsers
                .Where(x => x.EmployeeId > 0 && !string.IsNullOrWhiteSpace(x.DisplayName))
                .GroupBy(x => x.DisplayName.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    x => x.Key,
                    x => x.OrderBy(y => y.EmployeeId).Select(y => y.EmployeeId).First(),
                    StringComparer.OrdinalIgnoreCase);

            var employeeIdRemaps = new Dictionary<int, int>();

            foreach (var sourceUser in sourceUsers)
            {
                try
                {
                    if (sourceUser.EmployeeId <= 0)
                    {
                        errorCount++;

                        await _syncRunService.LogErrorAsync(
                            syncRunId: syncRunId,
                            sourceSystem: "economic_graph_users",
                            resourceName: "users",
                            errorStage: "validate",
                            errorMessage: "EmployeeId must be greater than zero.",
                            recordKey: sourceUser.UserPrincipalName,
                            rawPayload: System.Text.Json.JsonSerializer.Serialize(sourceUser),
                            cancellationToken: cancellationToken);

                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(sourceUser.UserPrincipalName))
                    {
                        sourceUser.UserPrincipalName = $"economic-{sourceUser.EmployeeId}@no-graph-match.local";
                    }

                    var resolvedManagerEmployeeId = ResolveManagerEmployeeId(
                        sourceUser,
                        sourceManagersByUserPrincipalName,
                        sourceManagersByDisplayName,
                        existingManagersByUserPrincipalName,
                        existingManagersByDisplayName);

                    var normalizedUserPrincipalName = sourceUser.UserPrincipalName.Trim();

                    if (existingUsersByEmployeeId.TryGetValue(sourceUser.EmployeeId, out var existingUser))
                    {
                        var oldSnapshot = BuildUserSnapshot(existingUser);

                        existingUser.UserPrincipalName = normalizedUserPrincipalName;
                        existingUser.DisplayName = sourceUser.DisplayName?.Trim() ?? string.Empty;
                        existingUser.OfficeLocation = sourceUser.OfficeLocation?.Trim();
                        existingUser.Department = sourceUser.Department?.Trim();
                        existingUser.EmployeeType = sourceUser.EmployeeType?.Trim();
                        existingUser.ManagerEmployeeId = resolvedManagerEmployeeId;
                        existingUser.SourceLastSyncedAt = DateTime.UtcNow;
                        existingUser.IsActive = sourceUser.IsActive ?? true;

                        existingUsersByUserPrincipalName[normalizedUserPrincipalName] = existingUser;

                        var newSnapshot = BuildUserSnapshot(existingUser);
                        if (System.Text.Json.JsonSerializer.Serialize(oldSnapshot) != System.Text.Json.JsonSerializer.Serialize(newSnapshot))
                        {
                            await _changeLogService.RecordChangeAsync(new RecordEntityChangeRequest
                            {
                                EventType = "UserSynced",
                                EventTitle = $"Bruger synkroniseret: {existingUser.DisplayName} ({existingUser.EmployeeId})",
                                EntityType = "User",
                                EntityId = existingUser.EmployeeId.ToString(),
                                OldSnapshot = oldSnapshot,
                                NewSnapshot = newSnapshot,
                                ChangeReason = "SyncUsers",
                                Caller = ResolveCaller(initiatedBy),
                                SourceModule = "UserSyncService"
                            }, cancellationToken);
                        }

                        rowsUpdated++;
                    }
                    else if (existingUsersByUserPrincipalName.TryGetValue(normalizedUserPrincipalName, out var existingUserByUserPrincipalName))
                    {
                        var previousEmployeeId = existingUserByUserPrincipalName.EmployeeId;

                        _db.Users.Remove(existingUserByUserPrincipalName);
                        existingUsersByEmployeeId.Remove(previousEmployeeId);

                        var replacementUser = new ExtUser
                        {
                            EmployeeId = sourceUser.EmployeeId,
                            UserPrincipalName = normalizedUserPrincipalName,
                            DisplayName = sourceUser.DisplayName?.Trim() ?? string.Empty,
                            OfficeLocation = sourceUser.OfficeLocation?.Trim(),
                            Department = sourceUser.Department?.Trim(),
                            EmployeeType = sourceUser.EmployeeType?.Trim(),
                            ManagerEmployeeId = resolvedManagerEmployeeId,
                            SourceLastSyncedAt = DateTime.UtcNow,
                            IsActive = sourceUser.IsActive ?? true
                        };

                        _db.Users.Add(replacementUser);

                        existingUsersByEmployeeId[sourceUser.EmployeeId] = replacementUser;
                        existingUsersByUserPrincipalName[normalizedUserPrincipalName] = replacementUser;
                        employeeIdRemaps[previousEmployeeId] = sourceUser.EmployeeId;

                        rowsUpdated++;
                    }
                    else
                    {
                        var newUser = new ExtUser
                        {
                            EmployeeId = sourceUser.EmployeeId,
                            UserPrincipalName = normalizedUserPrincipalName,
                            DisplayName = sourceUser.DisplayName?.Trim() ?? string.Empty,
                            OfficeLocation = sourceUser.OfficeLocation?.Trim(),
                            Department = sourceUser.Department?.Trim(),
                            EmployeeType = sourceUser.EmployeeType?.Trim(),
                            ManagerEmployeeId = resolvedManagerEmployeeId,
                            SourceLastSyncedAt = DateTime.UtcNow,
                            IsActive = sourceUser.IsActive ?? true
                        };

                        _db.Users.Add(newUser);
                        existingUsersByEmployeeId[sourceUser.EmployeeId] = newUser;
                        existingUsersByUserPrincipalName[normalizedUserPrincipalName] = newUser;
                        rowsInserted++;
                    }
                }
                catch (Exception ex)
                {
                    errorCount++;

                    await _syncRunService.LogErrorAsync(
                        syncRunId: syncRunId,
                        sourceSystem: "economic_graph_users",
                        resourceName: "users",  
                        errorStage: "map_upsert",
                        errorMessage: ex.Message,
                        recordKey: sourceUser.EmployeeId.ToString(),
                        rawPayload: System.Text.Json.JsonSerializer.Serialize(sourceUser),
                        cancellationToken: cancellationToken);
                }
            }

            if (employeeIdRemaps.Count > 0)
            {
                var trackedUsers = _db.ChangeTracker
                    .Entries<ExtUser>()
                    .Where(x => x.State != EntityState.Deleted)
                    .Select(x => x.Entity)
                    .ToList();

                foreach (var user in trackedUsers)
                {
                    if (user.ManagerEmployeeId.HasValue
                        && employeeIdRemaps.TryGetValue(user.ManagerEmployeeId.Value, out var remappedManagerEmployeeId))
                    {
                        user.ManagerEmployeeId = remappedManagerEmployeeId;
                    }
                }
            }

            foreach (var existingUser in existingUsers)
            {
                if (_db.Entry(existingUser).State == EntityState.Deleted)
                {
                    continue;
                }

                if (!sourceEmployeeIds.Contains(existingUser.EmployeeId) && existingUser.IsActive)
                {
                    var oldSnapshot = BuildUserSnapshot(existingUser);
                    existingUser.IsActive = false;
                    existingUser.SourceLastSyncedAt = DateTime.UtcNow;
                    rowsUpdated++;

                    await _changeLogService.RecordChangeAsync(new RecordEntityChangeRequest
                    {
                        EventType = "UserSynced",
                        EventTitle = $"Bruger deaktiveret: {existingUser.DisplayName} ({existingUser.EmployeeId})",
                        EntityType = "User",
                        EntityId = existingUser.EmployeeId.ToString(),
                        OldSnapshot = oldSnapshot,
                        NewSnapshot = BuildUserSnapshot(existingUser),
                        ChangeReason = "SyncUsers:NoLongerInSource",
                        Caller = ResolveCaller(initiatedBy),
                        SourceModule = "UserSyncService"
                    }, cancellationToken);
                }
            }

            await _db.SaveChangesAsync(cancellationToken);

            var status = errorCount > 0 ? "partial" : "succeeded";

            await _syncRunService.CompleteRunAsync(
                syncRunId: syncRunId,
                status: status,
                rowsRead: rowsRead,
                rowsInserted: rowsInserted,
                rowsUpdated: rowsUpdated,
                errorCount: errorCount,
                notes: "User sync completed",
                cancellationToken: cancellationToken);

            return new UserSyncResultDto
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
                syncRunId: syncRunId,
                sourceSystem: "economic_graph_users",
                resourceName: "users",
                errorStage: "fetch",
                errorMessage: ex.Message,
                cancellationToken: cancellationToken);

            await _syncRunService.CompleteRunAsync(
                syncRunId: syncRunId,
                status: "failed",
                rowsRead: rowsRead,
                rowsInserted: rowsInserted,
                rowsUpdated: rowsUpdated,
                errorCount: errorCount,
                notes: "User sync failed",
                cancellationToken: cancellationToken);

            throw;
        }
    }

    private static int? ResolveManagerEmployeeId(
        SourceUserDto sourceUser,
        IReadOnlyDictionary<string, int> sourceManagersByUserPrincipalName,
        IReadOnlyDictionary<string, int> sourceManagersByDisplayName,
        IReadOnlyDictionary<string, int> existingManagersByUserPrincipalName,
        IReadOnlyDictionary<string, int> existingManagersByDisplayName)
    {
        if (sourceUser.ManagerEmployeeId is > 0)
        {
            return sourceUser.ManagerEmployeeId;
        }

        if (int.TryParse(sourceUser.ManagerId, out var managerId) && managerId > 0)
        {
            return managerId;
        }

        if (!string.IsNullOrWhiteSpace(sourceUser.ManagerUserPrincipalName))
        {
            var managerUserPrincipalName = sourceUser.ManagerUserPrincipalName.Trim();

            if (sourceManagersByUserPrincipalName.TryGetValue(managerUserPrincipalName, out var sourceManagerEmployeeId))
            {
                return sourceManagerEmployeeId;
            }

            if (existingManagersByUserPrincipalName.TryGetValue(managerUserPrincipalName, out var existingManagerEmployeeId))
            {
                return existingManagerEmployeeId;
            }
        }

        var managerDisplayName = GetManagerDisplayName(sourceUser);

        if (!string.IsNullOrWhiteSpace(managerDisplayName))
        {
            if (sourceManagersByDisplayName.TryGetValue(managerDisplayName, out var sourceManagerEmployeeId))
            {
                return sourceManagerEmployeeId;
            }

            if (existingManagersByDisplayName.TryGetValue(managerDisplayName, out var existingManagerEmployeeId))
            {
                return existingManagerEmployeeId;
            }
        }

        return null;
    }

    private static string? GetManagerDisplayName(SourceUserDto sourceUser)
    {
        if (!string.IsNullOrWhiteSpace(sourceUser.ManagerName))
        {
            return sourceUser.ManagerName.Trim();
        }

        if (!string.IsNullOrWhiteSpace(sourceUser.Manager))
        {
            return sourceUser.Manager.Trim();
        }

        return null;
    }
}