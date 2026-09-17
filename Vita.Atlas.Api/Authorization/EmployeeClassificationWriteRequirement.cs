using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using Vita.Atlas.Application.Interfaces;
using Vita.Atlas.Domain.Enums;

namespace Vita.Atlas.Api.Authorization;

/// <summary>
/// Writing faglighed/profession. Satisfied by any one of three things, not all of them — hence a
/// requirement with the alternatives inside the handler rather than several policy clauses,
/// which ASP.NET Core would AND together.
/// </summary>
public sealed class EmployeeClassificationWriteRequirement : IAuthorizationRequirement
{
    public EmployeeClassificationWriteRequirement(string writeRole, string[] writeUserPrincipalNames)
    {
        WriteRole = writeRole;
        WriteUserPrincipalNames = writeUserPrincipalNames;
    }

    public string WriteRole { get; }

    public string[] WriteUserPrincipalNames { get; }
}

public sealed class EmployeeClassificationWriteHandler
    : AuthorizationHandler<EmployeeClassificationWriteRequirement>
{
    private readonly IUserAccessService _userAccessService;

    public EmployeeClassificationWriteHandler(IUserAccessService userAccessService)
    {
        _userAccessService = userAccessService;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        EmployeeClassificationWriteRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            return;
        }

        // Checked as a raw claim rather than via IsInRole: app roles arrive in the "roles"
        // claim, which is not the role claim type JwtBearer maps by default.
        var hasRole = context.User.Claims.Any(c =>
            (c.Type == "roles" || c.Type == ClaimTypes.Role) &&
            string.Equals(c.Value, requirement.WriteRole, StringComparison.OrdinalIgnoreCase));

        if (hasRole)
        {
            context.Succeed(requirement);
            return;
        }

        var upn = context.User.FindFirst("upn")?.Value
                  ?? context.User.FindFirst("preferred_username")?.Value;

        if (!string.IsNullOrWhiteSpace(upn) &&
            requirement.WriteUserPrincipalNames.Contains(upn, StringComparer.OrdinalIgnoreCase))
        {
            context.Succeed(requirement);
            return;
        }

        // The ordinary path since the access ladder exists: classification is one of the
        // employee properties a Manager may write. The two checks above are kept so an app-role
        // or allowlist grant configured before the ladder keeps working.
        if (await _userAccessService.ResolveRoleAsync(context.User) >= AccessRole.Manager)
        {
            context.Succeed(requirement);
        }
    }
}
