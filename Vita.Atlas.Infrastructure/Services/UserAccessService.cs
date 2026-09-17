using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using Vita.Atlas.Application.DTOs;
using Vita.Atlas.Application.Interfaces;
using Vita.Atlas.Domain.Enums;
using Vita.Atlas.Infrastructure.Data;
using Vita.Atlas.Infrastructure.Data.Entities;

namespace Vita.Atlas.Infrastructure.Services;

/// <summary>
/// Resolves what a user may do, from three sources in strict order of precedence:
///
///   1. the bootstrap admin allowlist in configuration — cannot be overridden from the UI;
///   2. an explicit row in core.user_access — set by an admin in the access pane;
///   3. the org chart — anyone with at least one active direct report in ext.users is a Manager.
///
/// Rule 3 is why the common case needs no administration at all: line managers get the employee
/// web part by virtue of being line managers. Rule 2 exists for the exceptions in both
/// directions — someone who manages nobody but needs access, someone who manages people but
/// should not have it.
/// </summary>
public sealed class UserAccessService : IUserAccessService
{
    private readonly AtlasDbContext _db;
    private readonly UserAccessSettings _settings;

    public UserAccessService(AtlasDbContext db, IOptions<UserAccessSettings> settings)
    {
        _db = db;
        _settings = settings.Value;
    }

    public async Task<AccessRole> ResolveRoleAsync(
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        var caller = CallerInfo.FromClaimsPrincipal(user);

        if (IsBootstrapAdmin(caller.Email))
        {
            return AccessRole.Admin;
        }

        var employee = await FindEmployeeAsync(caller.Email, cancellationToken);

        // An authenticated token we cannot tie to an employee row gets the floor, not a denial:
        // the caller still holds Planner.Access, so they keep the standard web parts.
        return employee is null
            ? AccessRole.Standard
            : await ResolveForEmployeeAsync(employee.EmployeeId, cancellationToken);
    }

    public async Task<CurrentUserAccessDto> GetCurrentAsync(
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        var caller = CallerInfo.FromClaimsPrincipal(user);
        var employee = await FindEmployeeAsync(caller.Email, cancellationToken);

        var role = IsBootstrapAdmin(caller.Email)
            ? AccessRole.Admin
            : employee is null
                ? AccessRole.Standard
                : await ResolveForEmployeeAsync(employee.EmployeeId, cancellationToken);

        return new CurrentUserAccessDto
        {
            EmployeeId = employee?.EmployeeId,
            DisplayName = employee?.DisplayName ?? caller.Name,
            UserPrincipalName = employee?.UserPrincipalName ?? caller.Email,
            Role = role,
            Capabilities = CapabilitiesFor(role)
        };
    }

    public async Task<IReadOnlyList<UserAccessDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        var employees = await _db.Users
            .AsNoTracking()
            .Where(u => u.IsActive)
            .Select(u => new
            {
                u.EmployeeId,
                u.DisplayName,
                u.UserPrincipalName,
                u.Department
            })
            .ToListAsync(cancellationToken);

        // Two set-based queries rather than a lookup per employee: the report counts that drive
        // every derived role, and the handful of explicit overrides.
        var reportCounts = await _db.Users
            .AsNoTracking()
            .Where(u => u.IsActive && u.ManagerEmployeeId != null)
            .GroupBy(u => u.ManagerEmployeeId!.Value)
            .Select(g => new { ManagerEmployeeId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ManagerEmployeeId, x => x.Count, cancellationToken);

        var overrides = await _db.UserAccess
            .AsNoTracking()
            .ToDictionaryAsync(x => x.EmployeeId, cancellationToken);

        return employees
            .Select(employee =>
            {
                reportCounts.TryGetValue(employee.EmployeeId, out var reportCount);
                overrides.TryGetValue(employee.EmployeeId, out var explicitGrant);

                return BuildDto(
                    employee.EmployeeId,
                    employee.DisplayName,
                    employee.UserPrincipalName,
                    employee.Department,
                    reportCount,
                    explicitGrant);
            })
            .OrderByDescending(x => x.EffectiveRole)
            .ThenBy(x => x.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public async Task<UserAccessDto> SetAsync(
        int employeeId,
        UpdateUserAccessRequest request,
        CallerInfo caller,
        CancellationToken cancellationToken = default)
    {
        var employee = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.EmployeeId == employeeId, cancellationToken)
            ?? throw new KeyNotFoundException($"Employee {employeeId} was not found.");

        // Editing a bootstrap admin would write a value that never takes effect, so it is
        // refused outright rather than stored and silently ignored.
        if (IsBootstrapAdmin(employee.UserPrincipalName))
        {
            throw new InvalidOperationException(
                $"{employee.DisplayName} er konfigureret som fast administrator og kan ikke ændres her.");
        }

        if (request.Role.HasValue && !Enum.IsDefined(request.Role.Value))
        {
            throw new InvalidOperationException($"Ukendt rolle: {(int)request.Role.Value}.");
        }

        // Self-demotion would take the access pane away from the person using it, leaving no way
        // back in short of the bootstrap allowlist or the database. Another admin can still
        // demote them; they just cannot do it to themselves by accident.
        var isSelf = !string.IsNullOrWhiteSpace(caller.Email) &&
                     string.Equals(caller.Email, employee.UserPrincipalName, StringComparison.OrdinalIgnoreCase);

        if (isSelf && request.Role != AccessRole.Admin)
        {
            throw new InvalidOperationException(
                "Du kan ikke fjerne din egen administratoradgang. Bed en anden administrator om at gøre det.");
        }

        var existing = await _db.UserAccess
            .FirstOrDefaultAsync(x => x.EmployeeId == employeeId, cancellationToken);

        var reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();

        if (request.Role is null)
        {
            // Clearing the override is not the same as demoting: the employee falls back to the
            // org chart, which for a line manager still means Manager.
            if (existing is not null)
            {
                _db.UserAccess.Remove(existing);
                await _db.SaveChangesAsync(cancellationToken);
            }
        }
        else if (existing is null)
        {
            _db.UserAccess.Add(new UserAccess
            {
                EmployeeId = employeeId,
                Role = request.Role.Value,
                Reason = reason,
                GrantedBy = caller.Email,
                GrantedAtUtc = DateTime.UtcNow
            });
            await _db.SaveChangesAsync(cancellationToken);
        }
        else
        {
            existing.Role = request.Role.Value;
            existing.Reason = reason;
            existing.UpdatedBy = caller.Email;
            existing.UpdatedAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
        }

        var reportCount = await CountActiveReportsAsync(employeeId, cancellationToken);
        var current = await _db.UserAccess
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.EmployeeId == employeeId, cancellationToken);

        return BuildDto(
            employee.EmployeeId,
            employee.DisplayName,
            employee.UserPrincipalName,
            employee.Department,
            reportCount,
            current);
    }

    private UserAccessDto BuildDto(
        int employeeId,
        string displayName,
        string? userPrincipalName,
        string? department,
        int reportCount,
        UserAccess? explicitGrant)
    {
        var derived = reportCount > 0 ? AccessRole.Manager : AccessRole.Standard;
        var isProtectedAdmin = IsBootstrapAdmin(userPrincipalName);

        return new UserAccessDto
        {
            EmployeeId = employeeId,
            DisplayName = displayName,
            UserPrincipalName = userPrincipalName,
            Department = department,
            DerivedRole = derived,
            EffectiveRole = isProtectedAdmin
                ? AccessRole.Admin
                : explicitGrant?.Role ?? derived,
            IsOverridden = !isProtectedAdmin && explicitGrant is not null,
            IsProtectedAdmin = isProtectedAdmin,
            Reason = explicitGrant?.Reason,
            DirectReportCount = reportCount,
            GrantedBy = explicitGrant?.GrantedBy,
            GrantedAtUtc = explicitGrant?.GrantedAtUtc
        };
    }

    private async Task<AccessRole> ResolveForEmployeeAsync(int employeeId, CancellationToken cancellationToken)
    {
        var explicitRole = await _db.UserAccess
            .AsNoTracking()
            .Where(x => x.EmployeeId == employeeId)
            .Select(x => (AccessRole?)x.Role)
            .FirstOrDefaultAsync(cancellationToken);

        if (explicitRole.HasValue)
        {
            return explicitRole.Value;
        }

        return await CountActiveReportsAsync(employeeId, cancellationToken) > 0
            ? AccessRole.Manager
            : AccessRole.Standard;
    }

    private Task<int> CountActiveReportsAsync(int employeeId, CancellationToken cancellationToken) =>
        _db.Users
            .AsNoTracking()
            .CountAsync(u => u.ManagerEmployeeId == employeeId && u.IsActive, cancellationToken);

    private Task<ExtUser?> FindEmployeeAsync(string? userPrincipalName, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(userPrincipalName))
        {
            return Task.FromResult<ExtUser?>(null);
        }

        return _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.UserPrincipalName == userPrincipalName, cancellationToken);
    }

    private bool IsBootstrapAdmin(string? userPrincipalName) =>
        !string.IsNullOrWhiteSpace(userPrincipalName) &&
        _settings.BootstrapAdminUserPrincipalNames.Contains(userPrincipalName, StringComparer.OrdinalIgnoreCase);

    // The role ladder expressed once, on the server. Each level includes everything below it.
    private static string[] CapabilitiesFor(AccessRole role) => role switch
    {
        AccessRole.Admin => ["timeEntry", "resourcePlan", "projects", "employees", "employeeWrite", "access"],
        AccessRole.Manager => ["timeEntry", "resourcePlan", "projects", "employees", "employeeWrite"],
        _ => ["timeEntry", "resourcePlan", "projects"]
    };
}
