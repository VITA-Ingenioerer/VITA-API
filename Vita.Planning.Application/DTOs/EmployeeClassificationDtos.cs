namespace Vita.Planning.Application.DTOs;

/// <summary>
/// An employee's classification as it currently stands in Microsoft Entra.
/// The extensionAttribute1-3 mirrors are deliberately absent: they are backend
/// infrastructure for Entra dynamic membership rules, not part of the API contract.
/// </summary>
public sealed class EmployeeClassificationDto
{
    public int EmployeeId { get; set; }

    public string UserPrincipalName { get; set; } = string.Empty;

    public string? DisplayName { get; set; }

    public string? PrimaryFaglighed { get; set; }

    public IReadOnlyList<string> SecondaryFagligheder { get; set; } = [];

    public string? Profession { get; set; }
}

public sealed class UpdateEmployeeClassificationRequest
{
    public string? PrimaryFaglighed { get; set; }

    public IReadOnlyList<string> SecondaryFagligheder { get; set; } = [];

    public string? Profession { get; set; }
}

/// <summary>
/// The allowed values, read from the Entra custom security attribute definitions rather
/// than hardcoded, so adding a value in Entra needs no frontend or backend deploy.
/// </summary>
public sealed class EmployeeClassificationOptionsDto
{
    public IReadOnlyList<string> PrimaryFagligheder { get; set; } = [];

    public IReadOnlyList<string> SecondaryFagligheder { get; set; } = [];

    public IReadOnlyList<string> Professions { get; set; } = [];
}

/// <summary>
/// Raw classification as Graph returned it, keyed by UPN. Internal to the Entra client.
/// </summary>
public sealed class EntraEmployeeClassification
{
    public string UserPrincipalName { get; set; } = string.Empty;

    public string? UserId { get; set; }

    public string? PrimaryFaglighed { get; set; }

    public IReadOnlyList<string> SecondaryFagligheder { get; set; } = [];

    public string? Profession { get; set; }
}

public sealed class EntraClassificationSettings
{
    // Settable (not init-only) because Program.cs fills these in after binding: the
    // classification client uses the same app registration as the other Graph clients, so
    // an unset value here inherits from the MicrosoftGraph section rather than forcing the
    // same tenant/client/secret to be configured twice.
    public string TenantId { get; set; } = string.Empty;

    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// Optional. When empty — and when the MicrosoftGraph section has no secret to inherit
    /// either — the client authenticates with the app service's managed identity instead,
    /// which is the preferred production configuration.
    /// </summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>Entra attribute set holding the three VITA attributes.</summary>
    public string AttributeSet { get; init; } = "VITA";

    public string PrimaryAttributeName { get; init; } = "PrimaryFaglighed";

    public string SecondaryAttributeName { get; init; } = "SecondaryFagligheder";

    public string ProfessionAttributeName { get; init; } = "Profession";

    /// <summary>
    /// How long the allowed-value taxonomy is cached in-process. Values change rarely and
    /// every classification write validates against them, so a short cache saves a Graph
    /// round-trip per save without making new values wait long to appear.
    /// </summary>
    public int OptionsCacheMinutes { get; init; } = 15;

    /// <summary>
    /// When false, the extensionAttribute1-3 mirror PATCH is skipped. The mirror only
    /// works for cloud-only users — for a directory-synced user, on-premises AD owns
    /// onPremisesExtensionAttributes and Graph rejects the write.
    /// </summary>
    public bool WriteExtensionAttributeMirror { get; init; } = true;
}
