using System.Security.Claims;

namespace Vita.Planning.Application.DTOs;

/// <summary>
/// Resolved from the AAD JWT token by the API layer.
/// Never trusted from the request body.
/// </summary>
public sealed class CallerInfo
{
    /// <summary>AAD Object ID (oid claim) — stable unique identifier.</summary>
    public string UserId { get; init; } = string.Empty;

    /// <summary>Display name (name claim).</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>UPN / email (upn or preferred_username claim).</summary>
    public string Email { get; init; } = string.Empty;

    public static CallerInfo FromClaimsPrincipal(ClaimsPrincipal user)
    {
        var oid = user.FindFirst("oid")?.Value
               ?? user.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value
               ?? string.Empty;

        var name = user.FindFirst("name")?.Value
                ?? user.FindFirst(ClaimTypes.Name)?.Value
                ?? string.Empty;

        var email = user.FindFirst("upn")?.Value
                 ?? user.FindFirst("preferred_username")?.Value
                 ?? user.FindFirst(ClaimTypes.Upn)?.Value
                 ?? user.FindFirst(ClaimTypes.Email)?.Value
                 ?? string.Empty;

        return new CallerInfo { UserId = oid, Name = name, Email = email };
    }

    public static CallerInfo System => new() { UserId = "system", Name = "System", Email = "system" };
}
