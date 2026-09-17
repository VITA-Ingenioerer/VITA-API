using System.Security.Claims;
using Vita.Atlas.Application.DTOs;
using Vita.Atlas.Domain.Enums;

namespace Vita.Atlas.Application.Interfaces;

public interface IUserAccessService
{
    /// <summary>
    /// The caller's effective role. Used by the authorization handler on every guarded request,
    /// so implementations are expected to be cheap.
    /// </summary>
    Task<AccessRole> ResolveRoleAsync(ClaimsPrincipal user, CancellationToken cancellationToken = default);

    /// <summary>The caller's own access, including the capability list the frontend renders from.</summary>
    Task<CurrentUserAccessDto> GetCurrentAsync(ClaimsPrincipal user, CancellationToken cancellationToken = default);

    /// <summary>
    /// One employee's access, for the access section on their card. Admin-only. Throws
    /// <see cref="KeyNotFoundException"/> for an unknown employee.
    /// </summary>
    Task<UserAccessDto> GetAsync(int employeeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets or clears one employee's override. Admin-only. Throws
    /// <see cref="KeyNotFoundException"/> for an unknown employee and
    /// <see cref="InvalidOperationException"/> when the target is a bootstrap admin.
    /// </summary>
    Task<UserAccessDto> SetAsync(
        int employeeId,
        UpdateUserAccessRequest request,
        CallerInfo caller,
        CancellationToken cancellationToken = default);
}
