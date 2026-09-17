using Microsoft.AspNetCore.Authorization;
using Vita.Atlas.Application.Interfaces;
using Vita.Atlas.Domain.Enums;

namespace Vita.Atlas.Api.Authorization;

/// <summary>
/// Satisfied when the caller's effective access role is at least <see cref="Role"/>. The ladder
/// is ordered, so one comparison covers "Manager or better" without enumerating the levels.
/// </summary>
public sealed class MinimumAccessRoleRequirement : IAuthorizationRequirement
{
    public MinimumAccessRoleRequirement(AccessRole role) => Role = role;

    public AccessRole Role { get; }
}

/// <summary>
/// Resolves the caller's role from the database on each guarded request.
///
/// Database-backed rather than claim-backed on purpose: a role granted in the access pane has to
/// take effect immediately. Reading it from the token would mean the grant did nothing until the
/// user signed out and back in, which is exactly the confusion the pane exists to avoid.
/// </summary>
public sealed class MinimumAccessRoleHandler : AuthorizationHandler<MinimumAccessRoleRequirement>
{
    private readonly IUserAccessService _userAccessService;

    public MinimumAccessRoleHandler(IUserAccessService userAccessService)
    {
        _userAccessService = userAccessService;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        MinimumAccessRoleRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            return;
        }

        var role = await _userAccessService.ResolveRoleAsync(context.User);

        if (role >= requirement.Role)
        {
            context.Succeed(requirement);
        }
    }
}
