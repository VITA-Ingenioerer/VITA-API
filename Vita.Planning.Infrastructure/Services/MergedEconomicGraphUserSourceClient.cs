using Microsoft.EntityFrameworkCore;
using Vita.Planning.Application.DTOs;
using Vita.Planning.Application.Interfaces;
using Vita.Planning.Infrastructure.Data;

namespace Vita.Planning.Infrastructure.Services;

public sealed class MergedEconomicGraphUserSourceClient : IInternalUserSourceClient
{
    private readonly PlanningDbContext _db;
    private readonly IMicrosoftGraphUserSourceClient _graphUserSourceClient;

    public MergedEconomicGraphUserSourceClient(
        PlanningDbContext db,
        IMicrosoftGraphUserSourceClient graphUserSourceClient)
    {
        _db = db;
        _graphUserSourceClient = graphUserSourceClient;
    }

    public async Task<IReadOnlyList<SourceUserDto>> GetUsersAsync(
        CancellationToken cancellationToken = default)
    {
        var economicEmployees = await _db.ProjectEmployees
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(x => new EconomicEmployeeForUserSync
            {
                EmployeeId = x.EmployeeNumber,
                Name = x.Name,
                GroupNumber = x.GroupNumber,
                IsActive = !x.IsBarred
            })
            .ToListAsync(cancellationToken);

        var graphUsers = await _graphUserSourceClient.GetUsersAsync(cancellationToken);

        var graphByEmployeeId = graphUsers
            .Where(x => TryParseEmployeeId(x.EmployeeId, out _))
            .GroupBy(x => int.Parse(x.EmployeeId!.Trim()))
            .ToDictionary(
                x => x.Key,
                x => x.OrderByDescending(y => y.AccountEnabled == true).First());

        var graphByDisplayName = graphUsers
            .Where(x => !string.IsNullOrWhiteSpace(x.DisplayName))
            .GroupBy(x => NormalizeName(x.DisplayName), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                x => x.Key,
                x => x.OrderByDescending(y => y.AccountEnabled == true).First(),
                StringComparer.OrdinalIgnoreCase);

        var sourceUsers = new List<SourceUserDto>();

        foreach (var economicEmployee in economicEmployees)
        {
            var graphUser = FindGraphUser(
                economicEmployee,
                graphByEmployeeId,
                graphByDisplayName);

            var userPrincipalName =
                FirstNonEmpty(
                    graphUser?.UserPrincipalName,
                    graphUser?.Mail);

            var displayName =
                FirstNonEmpty(
                    graphUser?.DisplayName,
                    economicEmployee.Name,
                    $"Employee {economicEmployee.EmployeeId}")!;

            sourceUsers.Add(new SourceUserDto
            {
                EmployeeId = economicEmployee.EmployeeId,
                UserPrincipalName = userPrincipalName ?? string.Empty,
                DisplayName = displayName,
                OfficeLocation = graphUser?.OfficeLocation,
                Department = graphUser?.Department,
                EmployeeType = graphUser?.EmployeeType,
                ManagerUserPrincipalName = graphUser?.ManagerUserPrincipalName,
                ManagerName = graphUser?.ManagerDisplayName,
                Manager = graphUser?.ManagerDisplayName,
                IsActive = ResolveIsActive(economicEmployee, graphUser)
            });
        }

        ResolveManagerEmployeeIds(sourceUsers);

        return sourceUsers
            .OrderBy(x => x.DisplayName)
            .ToList();
    }

    private static MicrosoftGraphUserDto? FindGraphUser(
        EconomicEmployeeForUserSync economicEmployee,
        IReadOnlyDictionary<int, MicrosoftGraphUserDto> graphByEmployeeId,
        IReadOnlyDictionary<string, MicrosoftGraphUserDto> graphByDisplayName)
    {
        if (graphByEmployeeId.TryGetValue(economicEmployee.EmployeeId, out var byEmployeeId))
        {
            return byEmployeeId;
        }

        var normalizedEconomicName = NormalizeName(economicEmployee.Name);

        if (!string.IsNullOrWhiteSpace(normalizedEconomicName)
            && graphByDisplayName.TryGetValue(normalizedEconomicName, out var byDisplayName))
        {
            return byDisplayName;
        }

        return null;
    }

    private static void ResolveManagerEmployeeIds(List<SourceUserDto> sourceUsers)
    {
        var usersByUserPrincipalName = sourceUsers
            .Where(x => !string.IsNullOrWhiteSpace(x.UserPrincipalName))
            .GroupBy(x => NormalizeKey(x.UserPrincipalName), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                x => x.Key,
                x => x.OrderBy(y => y.EmployeeId).First(),
                StringComparer.OrdinalIgnoreCase);

        foreach (var sourceUser in sourceUsers)
        {
            if (string.IsNullOrWhiteSpace(sourceUser.ManagerUserPrincipalName))
            {
                continue;
            }

            var managerKey = NormalizeKey(sourceUser.ManagerUserPrincipalName);

            if (usersByUserPrincipalName.TryGetValue(managerKey, out var manager))
            {
                sourceUser.ManagerEmployeeId = manager.EmployeeId;
            }
        }
    }

    private static bool ResolveIsActive(
        EconomicEmployeeForUserSync economicEmployee,
        MicrosoftGraphUserDto? graphUser)
    {
        if (!economicEmployee.IsActive)
        {
            return false;
        }

        if (graphUser?.AccountEnabled == false)
        {
            return false;
        }

        return true;
    }

    private static bool TryParseEmployeeId(string? value, out int employeeId)
    {
        return int.TryParse(value?.Trim(), out employeeId) && employeeId > 0;
    }

    private static string NormalizeKey(string? value)
    {
        return (value ?? string.Empty).Trim();
    }

    private static string NormalizeName(string? value)
    {
        var normalized = (value ?? string.Empty).Trim();

        if (normalized.EndsWith(" - VITA", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[..^7].Trim();
        }

        return normalized;
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return null;
    }

    private sealed class EconomicEmployeeForUserSync
    {
        public int EmployeeId { get; init; }

        public string Name { get; init; } = string.Empty;

        public int? GroupNumber { get; init; }

        public bool IsActive { get; init; }
    }
}