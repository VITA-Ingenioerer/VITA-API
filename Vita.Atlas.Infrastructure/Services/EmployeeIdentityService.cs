using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Vita.Atlas.Application.DTOs;
using Vita.Atlas.Application.Interfaces;
using Vita.Atlas.Infrastructure.Data;
using Vita.Atlas.Infrastructure.Data.Entities;

namespace Vita.Atlas.Infrastructure.Services;

/// <summary>
/// The single place that knows how VITA employee classification works end to end:
/// Entra is the authority, extensionAttribute1-3 are a derived mirror for dynamic
/// membership rules, and SQL is a read model that accelerates the planner.
///
/// Write direction is always Ressourceplan -> Graph -> SQL, never SQL -> Graph.
/// </summary>
public sealed class EmployeeIdentityService : IEmployeeIdentityService
{
    private const string OptionsCacheKey = "employee-classification-options";
    private const string BusinessEventType = "EmployeeClassificationUpdated";
    private const string BusinessEventEntityType = "Employee";

    private readonly AtlasDbContext _dbContext;
    private readonly IEntraEmployeeClassificationClient _entraClient;
    private readonly IBusinessEventService _businessEventService;
    private readonly IMemoryCache _cache;
    private readonly EntraClassificationSettings _settings;
    private readonly ILogger<EmployeeIdentityService> _logger;

    public EmployeeIdentityService(
        AtlasDbContext dbContext,
        IEntraEmployeeClassificationClient entraClient,
        IBusinessEventService businessEventService,
        IMemoryCache cache,
        IOptions<EntraClassificationSettings> settings,
        ILogger<EmployeeIdentityService> logger)
    {
        _dbContext = dbContext;
        _entraClient = entraClient;
        _businessEventService = businessEventService;
        _cache = cache;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<EmployeeClassificationDto> GetAsync(
        int employeeId,
        CancellationToken cancellationToken = default)
    {
        var user = await ResolveUserAsync(employeeId, cancellationToken);

        var classification = await _entraClient.GetAsync(user.UserPrincipalName, cancellationToken)
                             ?? throw new KeyNotFoundException(
                                 $"User {user.UserPrincipalName} was not found in Microsoft Entra.");

        // A read is also a reconciliation point: whatever Entra says now becomes the read
        // model, so opening an employee in the editor heals any drift for that employee.
        await SyncReadModelAsync(user.EmployeeId, classification, cancellationToken);

        return ToDto(user, classification);
    }

    public async Task<EmployeeClassificationDto> UpdateAsync(
        int employeeId,
        UpdateEmployeeClassificationRequest request,
        CallerInfo caller,
        CancellationToken cancellationToken = default)
    {
        var user = await ResolveUserAsync(employeeId, cancellationToken);

        var options = await GetOptionsAsync(cancellationToken);

        // Validate before touching Graph, so a bad request costs no Entra write and the
        // caller gets a 400 that names the offending value rather than a Graph 400.
        var validated = Validate(request, options);

        // The state before the write, for the audit trail. Read from Entra rather than the
        // local read model so the "old" side of the log is the real previous authority.
        var before = await _entraClient.GetAsync(user.UserPrincipalName, cancellationToken);

        var stored = await _entraClient.UpdateCustomSecurityAttributesAsync(
            user.UserPrincipalName,
            validated,
            cancellationToken);

        if (_settings.WriteExtensionAttributeMirror)
        {
            // The mirror is derived data — if it fails, the canonical attributes in Entra
            // are still correct and the next write or reconciliation will retry it. Failing
            // the whole request here would report a save as failed when it actually
            // succeeded, which is worse than a stale dynamic-group membership.
            try
            {
                await _entraClient.WriteExtensionAttributeMirrorAsync(
                    user.UserPrincipalName,
                    stored,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Employee classification for {UserPrincipalName} was saved in Entra, but mirroring it to extensionAttribute1-3 failed. Dynamic group membership may be stale until the next write or reconciliation.",
                    user.UserPrincipalName);
            }
        }

        await SyncReadModelAsync(user.EmployeeId, stored, cancellationToken);

        await RecordChangeAsync(user, before, stored, caller, cancellationToken);

        return ToDto(user, stored);
    }

    public async Task<EmployeeClassificationOptionsDto> GetOptionsAsync(
        CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(OptionsCacheKey, out EmployeeClassificationOptionsDto? cached) && cached is not null)
        {
            return cached;
        }

        var options = await _entraClient.GetAllowedValuesAsync(cancellationToken);

        _cache.Set(
            OptionsCacheKey,
            options,
            TimeSpan.FromMinutes(Math.Max(1, _settings.OptionsCacheMinutes)));

        return options;
    }

    public async Task<int> ReconcileAllAsync(CancellationToken cancellationToken = default)
    {
        var classifications = await _entraClient.GetAllAsync(cancellationToken);

        var byUpn = classifications
            .Where(x => !string.IsNullOrWhiteSpace(x.UserPrincipalName))
            .ToDictionary(x => x.UserPrincipalName, StringComparer.OrdinalIgnoreCase);

        var users = await _dbContext.Users
            .AsNoTracking()
            .Select(x => new { x.EmployeeId, x.UserPrincipalName })
            .ToListAsync(cancellationToken);

        var updated = 0;

        foreach (var user in users)
        {
            if (string.IsNullOrWhiteSpace(user.UserPrincipalName) ||
                !byUpn.TryGetValue(user.UserPrincipalName, out var classification))
            {
                continue;
            }

            if (await SyncReadModelAsync(user.EmployeeId, classification, cancellationToken))
            {
                updated++;
            }
        }

        _logger.LogInformation(
            "Employee classification reconciliation complete. {Updated} of {Total} employees changed.",
            updated,
            users.Count);

        return updated;
    }

    /// <summary>
    /// Enforces the classification rules and normalizes each value to the exact casing
    /// Entra holds, because the attributes are usePreDefinedValuesOnly and Graph rejects
    /// anything that is not a character-for-character match of an allowed value.
    /// </summary>
    private static UpdateEmployeeClassificationRequest Validate(
        UpdateEmployeeClassificationRequest request,
        EmployeeClassificationOptionsDto options)
    {
        var primary = ResolveAllowedValue(
            request.PrimaryFaglighed,
            options.PrimaryFagligheder,
            "Primær faglighed");

        if (primary is null)
        {
            throw new EmployeeClassificationValidationException(
                "Primær faglighed skal angives.");
        }

        var profession = ResolveAllowedValue(
            request.Profession,
            options.Professions,
            "Profession");

        if (profession is null)
        {
            throw new EmployeeClassificationValidationException(
                "Profession skal angives.");
        }

        var secondary = new List<string>();

        foreach (var value in request.SecondaryFagligheder)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            // Validated against the secondary attribute's own allowed values, not a merged
            // taxonomy: the two Entra definitions genuinely differ, and anything not in
            // this exact list is rejected by Graph.
            var resolved = ResolveAllowedValue(value, options.SecondaryFagligheder, "Sekundær faglighed")
                           ?? throw new EmployeeClassificationValidationException(
                               $"'{value}' er ikke en gyldig sekundær faglighed.");

            if (string.Equals(resolved, primary, StringComparison.OrdinalIgnoreCase))
            {
                throw new EmployeeClassificationValidationException(
                    $"'{resolved}' er allerede valgt som primær faglighed og kan ikke også være sekundær faglighed.");
            }

            if (!secondary.Contains(resolved, StringComparer.OrdinalIgnoreCase))
            {
                secondary.Add(resolved);
            }
        }

        return new UpdateEmployeeClassificationRequest
        {
            PrimaryFaglighed = primary,
            SecondaryFagligheder = secondary,
            Profession = profession
        };
    }

    /// <summary>
    /// Matches a submitted value against the allowed list, preferring an exact match and
    /// falling back to a case-insensitive one. Returns the allowed list's spelling, or
    /// null when the input was empty. Throws when the value is not allowed at all.
    /// </summary>
    private static string? ResolveAllowedValue(
        string? value,
        IReadOnlyList<string> allowed,
        string fieldLabel)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();

        var exact = allowed.FirstOrDefault(x => string.Equals(x, trimmed, StringComparison.Ordinal));

        if (exact is not null)
        {
            return exact;
        }

        var caseInsensitive = allowed
            .FirstOrDefault(x => string.Equals(x.Trim(), trimmed, StringComparison.OrdinalIgnoreCase));

        if (caseInsensitive is not null)
        {
            return caseInsensitive;
        }

        throw new EmployeeClassificationValidationException(
            $"{fieldLabel} '{trimmed}' findes ikke blandt de tilladte værdier i Entra.");
    }

    private async Task<ExtUser> ResolveUserAsync(int employeeId, CancellationToken cancellationToken)
    {
        var user = await _dbContext.Users
            .FirstOrDefaultAsync(x => x.EmployeeId == employeeId, cancellationToken)
            ?? throw new KeyNotFoundException($"Employee {employeeId} was not found.");

        // The UPN is resolved here, server-side, and never accepted from the caller — a
        // frontend that could name the target user could edit anyone's classification.
        if (string.IsNullOrWhiteSpace(user.UserPrincipalName))
        {
            throw new InvalidOperationException(
                $"Employee {employeeId} has no user principal name and cannot be classified.");
        }

        return user;
    }

    /// <summary>
    /// Replaces the local read model for one employee. Returns true when something
    /// actually changed, so reconciliation can report a meaningful count.
    /// </summary>
    private async Task<bool> SyncReadModelAsync(
        int employeeId,
        EntraEmployeeClassification classification,
        CancellationToken cancellationToken)
    {
        var user = await _dbContext.Users
            .FirstOrDefaultAsync(x => x.EmployeeId == employeeId, cancellationToken);

        if (user is null)
        {
            return false;
        }

        var existingSecondary = await _dbContext.UserSecondaryFagligheder
            .Where(x => x.EmployeeId == employeeId)
            .ToListAsync(cancellationToken);

        // The composite primary key lands in SQL Server's default case-insensitive
        // collation, so "El" and "EL" would collide on insert. Entra allows both to exist
        // as separate predefined values, so de-duplicate the same way SQL will compare.
        var incomingSecondary = classification.SecondaryFagligheder
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var changed =
            !string.Equals(user.PrimaryFaglighed, classification.PrimaryFaglighed, StringComparison.Ordinal) ||
            !string.Equals(user.Profession, classification.Profession, StringComparison.Ordinal) ||
            !existingSecondary
                .Select(x => x.Faglighed)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .SequenceEqual(
                    incomingSecondary.OrderBy(x => x, StringComparer.OrdinalIgnoreCase),
                    StringComparer.OrdinalIgnoreCase);

        if (!changed)
        {
            return false;
        }

        user.PrimaryFaglighed = classification.PrimaryFaglighed;
        user.Profession = classification.Profession;

        // Delete-then-insert rather than a diff: the list is at most a handful of rows per
        // employee, and replacing it wholesale cannot drift out of sync with Entra the way
        // an incremental merge can.
        _dbContext.UserSecondaryFagligheder.RemoveRange(existingSecondary);

        foreach (var faglighed in incomingSecondary)
        {
            _dbContext.UserSecondaryFagligheder.Add(new UserSecondaryFaglighed
            {
                EmployeeId = employeeId,
                Faglighed = faglighed
            });
        }

        // The primary/profession update and the secondary delete+insert are all staged on
        // the same change tracker, so this single SaveChangesAsync applies them in one
        // implicit transaction — an employee can never end up with the new primary and the
        // old secondary list.
        //
        // Deliberately NOT an explicit BeginTransactionAsync: the context is registered
        // with EnableRetryOnFailure (see Program.cs), and SqlServerRetryingExecutionStrategy
        // refuses user-initiated transactions outright — it cannot retry a block it does not
        // own. An explicit transaction here threw
        // "The configured execution strategy 'SqlServerRetryingExecutionStrategy' does not
        // support user-initiated transactions" on every save. The implicit transaction is
        // both retriable and sufficient.
        await _dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    private async Task RecordChangeAsync(
        ExtUser user,
        EntraEmployeeClassification? before,
        EntraEmployeeClassification after,
        CallerInfo caller,
        CancellationToken cancellationToken)
    {
        // These values will drive Entra dynamic groups and potentially Intune assignment,
        // so who changed whose classification has to be reconstructable later.
        await _businessEventService.RecordAsync(
            new RecordBusinessEventRequest
            {
                EventType = BusinessEventType,
                EventTitle = $"Klassifikation opdateret for {user.DisplayName}",
                EventDescription =
                    $"{caller.Name} ændrede faglighed/profession for {user.UserPrincipalName}.",
                EntityType = BusinessEventEntityType,
                EntityId = user.EmployeeId.ToString(),
                OldValue = Describe(before),
                NewValue = Describe(after),
                PayloadJson = JsonSerializer.Serialize(new
                {
                    employeeId = user.EmployeeId,
                    userPrincipalName = user.UserPrincipalName,
                    old = before is null
                        ? null
                        : new
                        {
                            primaryFaglighed = before.PrimaryFaglighed,
                            secondaryFagligheder = before.SecondaryFagligheder,
                            profession = before.Profession
                        },
                    @new = new
                    {
                        primaryFaglighed = after.PrimaryFaglighed,
                        secondaryFagligheder = after.SecondaryFagligheder,
                        profession = after.Profession
                    }
                }),
                CreatedByUserId = caller.UserId,
                CreatedByName = caller.Name,
                SourceModule = "employee-classification"
            },
            cancellationToken);
    }

    private static string Describe(EntraEmployeeClassification? classification)
    {
        if (classification is null)
        {
            return "Ingen klassifikation";
        }

        var secondary = classification.SecondaryFagligheder.Count == 0
            ? "ingen"
            : string.Join(", ", classification.SecondaryFagligheder);

        return $"Primær: {classification.PrimaryFaglighed ?? "ingen"}; " +
               $"Sekundære: {secondary}; " +
               $"Profession: {classification.Profession ?? "ingen"}";
    }

    private static EmployeeClassificationDto ToDto(ExtUser user, EntraEmployeeClassification classification) =>
        new()
        {
            EmployeeId = user.EmployeeId,
            UserPrincipalName = user.UserPrincipalName,
            DisplayName = user.DisplayName,
            PrimaryFaglighed = classification.PrimaryFaglighed,
            SecondaryFagligheder = classification.SecondaryFagligheder,
            Profession = classification.Profession
        };
}
