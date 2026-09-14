using Vita.Planning.Application.DTOs;

namespace Vita.Planning.Application.Interfaces;

/// <summary>
/// Orchestrates employee classification: validation, the Entra write, the
/// extensionAttribute mirror, the local read model and the audit trail. This is the only
/// place that knows how the three canonical attributes map onto extensionAttribute1-3.
/// </summary>
public interface IEmployeeIdentityService
{
    Task<EmployeeClassificationDto> GetAsync(
        int employeeId,
        CancellationToken cancellationToken = default);

    Task<EmployeeClassificationDto> UpdateAsync(
        int employeeId,
        UpdateEmployeeClassificationRequest request,
        CallerInfo caller,
        CancellationToken cancellationToken = default);

    Task<EmployeeClassificationOptionsDto> GetOptionsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Pulls every classification from Entra into the local read model. Used by the
    /// scheduled reconciliation so changes made directly in Entra don't leave the
    /// planner stale. Never writes back to Entra.
    /// </summary>
    Task<int> ReconcileAllAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Thin Microsoft Graph abstraction for the classification attributes. Knows the Graph
/// wire format and nothing about validation, auditing or SQL.
/// </summary>
public interface IEntraEmployeeClassificationClient
{
    Task<EntraEmployeeClassification?> GetAsync(
        string userPrincipalName,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EntraEmployeeClassification>> GetAllAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// PATCHes the custom security attributes, then re-reads them so the caller works
    /// from what Entra actually stored rather than from what was requested.
    /// </summary>
    Task<EntraEmployeeClassification> UpdateCustomSecurityAttributesAsync(
        string userPrincipalName,
        UpdateEmployeeClassificationRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Mirrors the canonical values into onPremisesExtensionAttributes 1-3 so Entra
    /// dynamic membership rules can use them — dynamic groups cannot read custom
    /// security attributes.
    /// </summary>
    Task WriteExtensionAttributeMirrorAsync(
        string userPrincipalName,
        EntraEmployeeClassification classification,
        CancellationToken cancellationToken = default);

    Task<EmployeeClassificationOptionsDto> GetAllowedValuesAsync(
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Thrown when a requested classification fails validation. Surfaces as HTTP 400.
/// </summary>
public sealed class EmployeeClassificationValidationException : Exception
{
    public EmployeeClassificationValidationException(string message) : base(message)
    {
    }
}
