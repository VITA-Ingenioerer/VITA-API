using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Vita.Planning.Application.DTOs;
using Vita.Planning.Application.Interfaces;
using Vita.Planning.Infrastructure.Data;
using Vita.Planning.Infrastructure.Data.Entities;

namespace Vita.Planning.Api.Controllers;

[ApiController]
[Route("api/import")]
public sealed class LegacyImportController : ControllerBase
{
    private const string BillableProjectTargetType = "BillableProject";
    private const string OfferTargetType = "Offer";
    // Must match CK_core_planning_targets_type_matches_source, which checks target_type = 'Internal'
    // (not 'InternalPlanningCode') for rows backed by internal_planning_code_id.
    private const string InternalPlanningCodeTargetType = "Internal";

    private static readonly Regex OfferPattern = new(@"^T\d{5}$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex ProjectWithSuffixPattern = new(@"^(?<projectNumber>\d+)[_-].+$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly HashSet<string> TrueInternalProjectCodes =
    [
        "210001",
        "210002",
        "210003",
        "210004",
        "210005",
        "210006",
        "210010",
        "210011",
        "210012",
        "210014",
        "210015",
        "210016",
        "210018",
        "210019",
        "210020",
        "210021",
        "210022",
        "210023"
    ];

    // The legacy Ressourceplan API sends these internal codes as names instead of the
    // numeric codes used everywhere else. Map them to the real code so they resolve to the
    // same planning target as any other feed that already sends "210001"/"210002"/"210005",
    // instead of silently creating a second, disconnected internal planning code.
    private static readonly Dictionary<string, (string Code, string Name)> LegacyProjectCodeAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ADMIN"] = ("210001", "Admin"),
        ["SALG"] = ("210002", "Salg"),
        ["FRAVÆR"] = ("210005", "Fravær")
    };

    private readonly PlanningDbContext _dbContext;
    private readonly IRessourceplanWorkbookSourceClient _workbookSource;
    private readonly IProjectLifecycleLogService _lifecycleLog;
    private readonly IResourcePlanEntryHistoryService _historyService;
    private readonly IEntityChangeLogService _changeLog;
    private readonly IActivityMatchingService _activityMatching;

    public LegacyImportController(
        PlanningDbContext dbContext,
        IRessourceplanWorkbookSourceClient workbookSource,
        IProjectLifecycleLogService lifecycleLog,
        IResourcePlanEntryHistoryService historyService,
        IEntityChangeLogService changeLog,
        IActivityMatchingService activityMatching)
    {
        _dbContext = dbContext;
        _workbookSource = workbookSource;
        _lifecycleLog = lifecycleLog;
        _historyService = historyService;
        _changeLog = changeLog;
        _activityMatching = activityMatching;
    }

    /// <summary>
    /// Reads the Ressourceplane Excel workbook directly from SharePoint (via Microsoft Graph)
    /// and imports it into resource_plan_entries. Unknown projects are created as internal
    /// planning codes; projects matching TYYXXX (e.g. T26001) are created as offers.
    /// </summary>
    [HttpPost("legacy-ressourceplan")]
    public async Task<ActionResult<LegacyImportResult>> Import(
        [FromBody] LegacyImportRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var invalidPlanningTargets = new List<LegacyInvalidPlanningTargetInfo>();
            var unsyncedProjectCodes = new List<string>();
            var unresolvedInitials = new List<string>();

            // 1. Verify scenario exists; if ScenarioId is 0, fall back to the default scenario
            ResourcePlanScenario? scenario;
            if (request.ScenarioId == 0)
            {
                scenario = await _dbContext.ResourcePlanScenarios
                    .FirstOrDefaultAsync(s => s.IsDefault, cancellationToken);

                if (scenario is null)
                {
                    return BadRequest(new { message = "No default scenario found. Please provide a ScenarioId." });
                }
            }
            else
            {
                scenario = await _dbContext.ResourcePlanScenarios
                    .FirstOrDefaultAsync(s => s.ScenarioId == request.ScenarioId, cancellationToken);

                if (scenario is null)
                {
                    return BadRequest(new { message = $"Scenario {request.ScenarioId} not found." });
                }
            }

            var resolvedScenarioId = scenario.ScenarioId;

            // 2. Fetch rows straight from the Ressourceplane workbook (SharePoint via Graph)
            IReadOnlyList<RessourceplanWorkbookRowDto> workbookRows;
            try
            {
                workbookRows = await _workbookSource.GetRowsAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                return StatusCode(502, new { message = $"Failed to read Ressourceplane workbook: {ex.Message}" });
            }

            if (workbookRows.Count == 0)
            {
                return Ok(new LegacyImportResult { Message = "No rows found in the Ressourceplane workbook." });
            }

            // Resolve each row's Initials against ext.users (email prefix, e.g. "thom" -> thom@vitaing.dk).
            // Anything that doesn't resolve is reported instead of silently dropped, since a bad
            // lookup here was the reason a prior import came back with far fewer rows than expected.
            var activeUsers = await _dbContext.Users
                .AsNoTracking()
                .Where(u => u.IsActive)
                .Select(u => new { u.EmployeeId, u.UserPrincipalName })
                .ToListAsync(cancellationToken);

            var employeeIdByEmailPrefix = activeUsers
                .Where(u => !string.IsNullOrWhiteSpace(u.UserPrincipalName) && u.UserPrincipalName.Contains('@'))
                .GroupBy(u => u.UserPrincipalName.Split('@')[0].Trim().ToLowerInvariant())
                .ToDictionary(g => g.Key, g => g.First().EmployeeId);

            // Placeholder/unstaffed-role rows (e.g. "NN-HVAC") never match a real user by design —
            // they resolve against core.virtual_resources by code instead, so those hours land on
            // a virtual-resource-backed resource plan instead of being dropped as unresolved.
            var virtualResourceIdByCode = await _dbContext.VirtualResources
                .AsNoTracking()
                .Where(v => v.IsActive)
                .ToDictionaryAsync(v => v.Code, v => v.VirtualResourceId, StringComparer.OrdinalIgnoreCase, cancellationToken);

            var items = new List<LegacyRessourceplanItem>();

            foreach (var row in workbookRows)
            {
                int? employeeId = null;
                int? virtualResourceId = null;

                if (employeeIdByEmailPrefix.TryGetValue(row.Initials, out var resolvedEmployeeId))
                {
                    employeeId = resolvedEmployeeId;
                }
                else if (virtualResourceIdByCode.TryGetValue(row.Initials, out var resolvedVirtualResourceId))
                {
                    virtualResourceId = resolvedVirtualResourceId;
                }
                else
                {
                    if (!unresolvedInitials.Contains(row.Initials, StringComparer.OrdinalIgnoreCase))
                    {
                        unresolvedInitials.Add(row.Initials);
                    }

                    continue;
                }

                foreach (var monthHour in row.MonthHours)
                {
                    items.Add(new LegacyRessourceplanItem
                    {
                        Id = 0,
                        EmployeeId = employeeId,
                        VirtualResourceId = virtualResourceId,
                        Project = row.Project,
                        MonthYear = monthHour.MonthYear,
                        Hours = monthHour.Hours,
                        Description = row.Description,
                        CreatedAt = DateTime.UtcNow
                    });
                }
            }

            if (items.Count == 0)
            {
                return Ok(new LegacyImportResult
                {
                    Message = "No rows resolved to a known user.",
                    UnresolvedInitials = unresolvedInitials
                });
            }

            // The workbook commonly has multiple rows for the same employee/project/month
            // (e.g. logged against different activities), but a resource_plan_entries row only
            // holds one Hours/Description/ProjectActivityId per employee/project/day. Merge those
            // rows here (summing hours, combining descriptions) so the later per-key dedupe below
            // is a safety net rather than the thing silently dropping real hours.
            //
            // Group by the alias-normalized code (e.g. "ADMIN" and "210001" both become "210001"),
            // not the raw text: "ADMIN"/"210001" resolve to the same planning target in step 3, so
            // grouping by raw text would let the two spellings collide on the same
            // (resourcePlanId, planningTargetId, year, month) key later and get silently dropped
            // instead of merged — this was quietly eating most admin/vacation hours, since those
            // are exactly the codes fed as both a name and a number depending on the source row.
            items = items
                .GroupBy(i => (
                    i.EmployeeId.HasValue,
                    ResourceId: i.EmployeeId ?? i.VirtualResourceId,
                    Project: NormalizeProjectCodeForGrouping(i.Project),
                    i.MonthYear))
                .Select(g =>
                {
                    var first = g.First();
                    var descriptions = g
                        .Select(i => i.Description)
                        .Where(d => !string.IsNullOrWhiteSpace(d))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    // Prefer an alias-form spelling (e.g. "ADMIN") as the surviving Project text so
                    // ResolvePlanningTargetAsync still creates a friendly name ("Admin") the first
                    // time this internal code is ever seen, instead of falling back to "210001".
                    var aliasedMember = g.FirstOrDefault(i => LegacyProjectCodeAliases.ContainsKey(i.Project));
                    var surviving = aliasedMember ?? first;

                    return new LegacyRessourceplanItem
                    {
                        Id = 0,
                        EmployeeId = first.EmployeeId,
                        VirtualResourceId = first.VirtualResourceId,
                        Project = surviving.Project,
                        MonthYear = first.MonthYear,
                        Hours = g.Sum(i => i.Hours),
                        Description = descriptions.Count == 0 ? null : string.Join("; ", descriptions),
                        CreatedAt = first.CreatedAt
                    };
                })
                .ToList();

            // 3. Resolve planning targets (one per unique project code)
            var planningTargetByCode = new Dictionary<string, PlanningTarget>(StringComparer.OrdinalIgnoreCase);

            var uniqueCodes = items.Select(i => i.Project).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

            foreach (var code in uniqueCodes)
            {
                var target = await TryResolvePlanningTargetAsync(code, invalidPlanningTargets, unsyncedProjectCodes, cancellationToken);

                if (target is not null)
                {
                    planningTargetByCode[code] = target;
                }
            }

            // 4. Resolve resource plans (one per unique employee/virtual resource)
            var resourcePlanByResourceKey = new Dictionary<(bool IsVirtual, int ResourceId), ResourcePlan>();

            var uniqueResourceKeys = items
                .Where(i => planningTargetByCode.ContainsKey(i.Project))
                .Select(i => (IsVirtual: i.VirtualResourceId.HasValue, ResourceId: i.EmployeeId ?? i.VirtualResourceId!.Value))
                .Distinct()
                .ToList();

            foreach (var resourceKey in uniqueResourceKeys)
            {
                var plan = resourceKey.IsVirtual
                    ? await ResolveResourcePlanForVirtualResourceAsync(resourceKey.ResourceId, resolvedScenarioId, cancellationToken)
                    : await ResolveResourcePlanAsync(resourceKey.ResourceId, resolvedScenarioId, cancellationToken);

                resourcePlanByResourceKey[resourceKey] = plan;
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            // Load every existing entry for the resource plans involved in one query instead of
            // one query per business day per item — the per-day round trip was the actual reason
            // a full sync took so long once the earlier bugs stopped silently skipping most rows.
            var relevantResourcePlanIds = resourcePlanByResourceKey.Values
                .Select(p => p.ResourcePlanId)
                .Distinct()
                .ToList();

            var existingEntriesByKey = await _dbContext.ResourcePlanEntries
                .Where(e => relevantResourcePlanIds.Contains(e.ResourcePlanId))
                .ToDictionaryAsync(
                    e => (e.ResourcePlanId, e.PlanningTargetId, e.PlanDate),
                    cancellationToken);

            // 5. Import entries (upsert: create new, update changed hours/description)
            int created = 0;
            int updated = 0;
            int skipped = 0;

            var seenKeys = new HashSet<(int resourcePlanId, int planningTargetId, int year, int month)>();
            var historyRecords = new List<RecordResourcePlanEntryHistoryRequest>();
            var pendingNewEntries = new List<(ResourcePlanEntry Entry, ResourcePlan Plan)>();
            var importCorrelationId = Guid.NewGuid();

            foreach (var item in items)
            {
                if (!TryParseMonthYear(item.MonthYear, out int year, out int month))
                {
                    skipped++;
                    continue;
                }

                if (!planningTargetByCode.TryGetValue(item.Project, out var planningTarget))
                {
                    skipped++;
                    continue;
                }

                var resourcePlan = resourcePlanByResourceKey[
                    (item.VirtualResourceId.HasValue, item.EmployeeId ?? item.VirtualResourceId!.Value)];

                var businessDays = GetBusinessDaysForMonth(year, month);

                if (businessDays.Count == 0)
                {
                    skipped++;
                    continue;
                }

                var key = (resourcePlan.ResourcePlanId, planningTarget.PlanningTargetId, year, month);

                if (seenKeys.Contains(key))
                {
                    skipped++;
                    continue;
                }

                seenKeys.Add(key);

                var normalizedDescription = string.IsNullOrWhiteSpace(item.Description) ? null : item.Description.Trim();
                var distributedHours = DistributeHoursAcrossBusinessDays(item.Hours, businessDays.Count);

                // Match once per item (same project/description/employee for every day it spans):
                // 1. description against the project's assigned e-conomic activities, 2. the
                // employee's department against the same list, 3. otherwise leave unmatched.
                var matchedProjectActivityId = await _activityMatching.MatchActivityAsync(
                    planningTarget.PlanningTargetId,
                    normalizedDescription,
                    resourcePlan.EmployeeId,
                    cancellationToken);

                for (var dayIndex = 0; dayIndex < businessDays.Count; dayIndex++)
                {
                    var planDate = businessDays[dayIndex];
                    var distributedHoursForDay = distributedHours[dayIndex];

                    existingEntriesByKey.TryGetValue(
                        (resourcePlan.ResourcePlanId, planningTarget.PlanningTargetId, planDate),
                        out var existing);

                    if (existing is not null)
                    {
                        if (existing.Hours != distributedHoursForDay
                            || existing.Description != normalizedDescription
                            || existing.ProjectActivityId != matchedProjectActivityId)
                        {
                            var oldHours = existing.Hours;
                            var oldDescription = existing.Description;

                            existing.Hours = distributedHoursForDay;
                            existing.Description = normalizedDescription;
                            existing.ProjectActivityId = matchedProjectActivityId;
                            existing.UpdatedBy = "legacy-import";
                            existing.UpdatedAt = DateTime.UtcNow;
                            updated++;

                            // resource_plan_entry_histories.employee_id is still required and doesn't
                            // support virtual-resource-backed plans yet; skip the audit record for those
                            // rather than persisting a bogus employee id. The entry itself is still saved.
                            if (resourcePlan.EmployeeId.HasValue)
                            {
                                historyRecords.Add(new RecordResourcePlanEntryHistoryRequest
                                {
                                    ResourcePlanEntryId = existing.ResourcePlanEntryId,
                                    ResourcePlanId = existing.ResourcePlanId,
                                    EmployeeId = resourcePlan.EmployeeId.Value,
                                    ScenarioId = resourcePlan.ScenarioId,
                                    PlanningTargetId = existing.PlanningTargetId,
                                    PlanDate = existing.PlanDate,
                                    OldHours = oldHours,
                                    NewHours = distributedHoursForDay,
                                    OldDescription = oldDescription,
                                    NewDescription = normalizedDescription,
                                    ChangeType = "Updated",
                                    ChangeReason = "LegacyImport",
                                    ChangedByUserId = "legacy-import",
                                    ChangedByName = "Legacy Import",
                                    SourceModule = "LegacyImportController",
                                    CorrelationId = importCorrelationId
                                });
                            }
                        }
                        else
                        {
                            skipped++;
                        }

                        continue;
                    }

                    var entry = new ResourcePlanEntry
                    {
                        ResourcePlanId = resourcePlan.ResourcePlanId,
                        PlanningTargetId = planningTarget.PlanningTargetId,
                        Hours = distributedHoursForDay,
                        Description = normalizedDescription,
                        ProjectActivityId = matchedProjectActivityId,
                        IsManualOverride = false,
                        CreatedBy = "legacy-import",
                        PlanDate = planDate,
                        CreatedAt = item.CreatedAt
                    };

                    _dbContext.ResourcePlanEntries.Add(entry);
                    pendingNewEntries.Add((entry, resourcePlan));
                    created++;
                }
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            foreach (var (entry, plan) in pendingNewEntries)
            {
                if (!plan.EmployeeId.HasValue)
                {
                    continue;
                }

                historyRecords.Add(new RecordResourcePlanEntryHistoryRequest
                {
                    ResourcePlanEntryId = entry.ResourcePlanEntryId,
                    ResourcePlanId = entry.ResourcePlanId,
                    EmployeeId = plan.EmployeeId.Value,
                    ScenarioId = plan.ScenarioId,
                    PlanningTargetId = entry.PlanningTargetId,
                    PlanDate = entry.PlanDate,
                    OldHours = null,
                    NewHours = entry.Hours,
                    OldDescription = null,
                    NewDescription = entry.Description,
                    ChangeType = "Created",
                    ChangeReason = "LegacyImport",
                    ChangedByUserId = "legacy-import",
                    ChangedByName = "Legacy Import",
                    SourceModule = "LegacyImportController",
                    CorrelationId = importCorrelationId
                });
            }

            foreach (var record in historyRecords)
            {
                await _historyService.RecordAsync(record, cancellationToken);
            }

            await _changeLog.RecordChangeAsync(new RecordEntityChangeRequest
            {
                EventType = "LegacyResourcePlanImportRun",
                EventTitle = $"Legacy ressourceplan import: {created} created, {updated} updated, {skipped} skipped",
                EntityType = "LegacyImport",
                EntityId = importCorrelationId.ToString(),
                NewValue = $"{created} created / {updated} updated / {skipped} skipped",
                NewSnapshot = new
                {
                    Created = created,
                    Updated = updated,
                    Skipped = skipped,
                    UnsyncedProjectCodes = unsyncedProjectCodes,
                    UnresolvedInitials = unresolvedInitials,
                    InvalidTargetTypeCount = invalidPlanningTargets.Count
                },
                ChangeReason = "LegacyImport",
                Caller = CallerInfo.System,
                SourceModule = "LegacyImportController",
                CorrelationId = importCorrelationId
            }, cancellationToken);

            return Ok(new LegacyImportResult
            {
                Message = invalidPlanningTargets.Count == 0
                    ? "Import complete."
                    : "Import complete with skipped planning targets that violated CK_core_planning_targets_target_type.",
                Created = created,
                Updated = updated,
                Skipped = skipped,
                InvalidTargetTypes = invalidPlanningTargets,
                UnsyncedProjectCodes = unsyncedProjectCodes,
                UnresolvedInitials = unresolvedInitials
            });
        }
        catch (DbUpdateException ex) when (IsInvalidPlanningTargetTypeFailure(ex))
        {
            var invalidTargets = GetPendingPlanningTargetTypes();

            return BadRequest(new
            {
                message = "Legacy import failed because one or more planning targets used a target_type value that violates CK_core_planning_targets_target_type.",
                constraint = "CK_core_planning_targets_target_type",
                invalidTargetTypes = invalidTargets
            });
        }
    }

    // Same-target codes ("ADMIN" and "210001", "FRAVÆR" and "210005", ...) must group together
    // during item aggregation, or one spelling silently collides with and drops the other later.
    private static string NormalizeProjectCodeForGrouping(string project)
    {
        return LegacyProjectCodeAliases.TryGetValue(project, out var alias)
            ? alias.Code
            : project.ToUpperInvariant();
    }

    private async Task<PlanningTarget> ResolvePlanningTargetAsync(
        string code,
        List<string> unsyncedProjectCodes,
        CancellationToken cancellationToken)
    {
        string? aliasFriendlyName = null;

        if (LegacyProjectCodeAliases.TryGetValue(code, out var alias))
        {
            code = alias.Code;
            aliasFriendlyName = alias.Name;
        }

        // Check if a planning target already exists for this code
        var existing = await _dbContext.PlanningTargets
            .FirstOrDefaultAsync(t => t.Code == code.ToUpperInvariant(), cancellationToken);

        if (existing is not null)
        {
            return existing;
        }

        if (OfferPattern.IsMatch(code))
        {
            return await ResolveOfferTargetAsync(code, cancellationToken);
        }

        if (int.TryParse(code, out int projectNumber))
        {
            // If this is a true internal project code, treat as internal
            if (TrueInternalProjectCodes.Contains(code))
            {
                return await ResolveInternalTargetAsync(code, cancellationToken, name: aliasFriendlyName);
            }
            // Otherwise, try to resolve as a real project
            return await ResolveProjectTargetAsync(projectNumber, unsyncedProjectCodes, cancellationToken);
        }

        var suffixedProjectNumber = await TryResolveSuffixedProjectNumberAsync(code, cancellationToken);

        if (suffixedProjectNumber.HasValue)
        {
            return await ResolveProjectTargetAsync(suffixedProjectNumber.Value, unsyncedProjectCodes, cancellationToken);
        }

        // Not a true internal project, not an offer, not a real project: treat as internal planning code
        return await ResolveUnknownTargetAsync(code, cancellationToken);

    }

    private async Task<PlanningTarget?> TryResolvePlanningTargetAsync(
        string code,
        List<LegacyInvalidPlanningTargetInfo> invalidPlanningTargets,
        List<string> unsyncedProjectCodes,
        CancellationToken cancellationToken)
    {
        try
        {
            var target = await ResolvePlanningTargetAsync(code, unsyncedProjectCodes, cancellationToken);

            if (_dbContext.Entry(target).State == EntityState.Added)
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            return target;
        }
        catch (DbUpdateException ex) when (IsInvalidPlanningTargetTypeFailure(ex))
        {
            invalidPlanningTargets.AddRange(GetPendingPlanningTargetTypes());
            DetachPendingPlanningTargets();
            return null;
        }
    }

    // Unknown legacy codes are imported as internal planning codes so they fit the valid planning target types.
    private async Task<PlanningTarget> ResolveUnknownTargetAsync(string code, CancellationToken cancellationToken)
    {
        return await ResolveInternalTargetAsync(code, cancellationToken, isPlannable: false);
    }

    private async Task<PlanningTarget> ResolveProjectTargetAsync(
        int projectNumber,
        List<string> unsyncedProjectCodes,
        CancellationToken cancellationToken)
    {
        var existingTarget = await _dbContext.PlanningTargets
            .FirstOrDefaultAsync(
                t => t.TargetType == BillableProjectTargetType && t.ExtProjectNumber == projectNumber,
                cancellationToken);

        if (existingTarget is not null)
        {
            return existingTarget;
        }

        var project = await _dbContext.Projects
            .FirstOrDefaultAsync(p => p.ProjectNumber == projectNumber, cancellationToken);

        // If the project doesn't exist in the local mirror we cannot satisfy FK_core_planning_targets_project.
        // Fall back to an internal planning code so the entry can still be imported, but tell the caller
        // so they can run a project sync and re-import instead of silently mis-filing real project hours.
        if (project is null)
        {
            unsyncedProjectCodes.Add(projectNumber.ToString());
            return await ResolveUnknownTargetAsync(projectNumber.ToString(), cancellationToken);
        }

        var code = projectNumber.ToString();

        var target = new PlanningTarget
        {
            Code = code,
            Name = project.ProjectName ?? code,
            TargetType = BillableProjectTargetType,
            ExtProjectNumber = projectNumber,
            IsActive = true,
            IsPlannable = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        _dbContext.PlanningTargets.Add(target);
        return target;
    }

    private async Task<PlanningTarget> ResolveOfferTargetAsync(string code, CancellationToken cancellationToken)
    {
        var upperCode = code.ToUpperInvariant();

        var offer = await _dbContext.Offers
            .FirstOrDefaultAsync(o => o.OfferNumber == upperCode, cancellationToken);

        if (offer is null)
        {
            offer = new Offer
            {
                OfferNumber = upperCode,
                Title = upperCode,
                OfferStatusId = await ResolveLegacyOfferStatusIdAsync("Open", cancellationToken),
                IsActive = true,
                CreatedBy = "legacy-import",
                CreatedAtUtc = DateTime.UtcNow
            };
            _dbContext.Offers.Add(offer);
            await _dbContext.SaveChangesAsync(cancellationToken);

            await _lifecycleLog.CreateAsync(new CreateProjectLifecycleLogRequest
            {
                TargetType = "Offer",
                OfferId = offer.OfferId,
                EventType = "OfferCreated",
                EventTitle = "Offer created by legacy import",
                NewValue = upperCode,
                CreatedBy = "legacy-import"
            }, cancellationToken);
        }

        var target = new PlanningTarget
        {
            Code = upperCode,
            Name = upperCode,
            TargetType = OfferTargetType,
            OfferId = offer.OfferId,
            IsActive = true,
            IsPlannable = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        _dbContext.PlanningTargets.Add(target);
        return target;
    }

    private async Task<PlanningTarget> ResolveInternalTargetAsync(
        string code,
        CancellationToken cancellationToken,
        bool isPlannable = true,
        string? name = null)
    {
        var upperCode = code.ToUpperInvariant();

        var internalCode = await _dbContext.InternalPlanningCodes
            .FirstOrDefaultAsync(c => c.Code == upperCode, cancellationToken);

        if (internalCode is null)
        {
            internalCode = new InternalPlanningCode
            {
                Code = upperCode,
                Name = name ?? upperCode,
                Category = "Other",
                IsActive = true,
                IsPlannable = true,
                IsInternal = true,
                IsAbsence = false,
                IsBillable = false,
                CreatedBy = "legacy-import",
                CreatedAtUtc = DateTime.UtcNow
            };
            _dbContext.InternalPlanningCodes.Add(internalCode);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        var target = new PlanningTarget
        {
            Code = upperCode,
            Name = internalCode.Name,
            TargetType = InternalPlanningCodeTargetType,
            InternalPlanningCodeId = internalCode.InternalPlanningCodeId,
            IsActive = true,
            IsPlannable = isPlannable,
            CreatedAtUtc = DateTime.UtcNow
        };

        _dbContext.PlanningTargets.Add(target);
        return target;
    }
    private async Task<int?> ResolveLegacyOfferStatusIdAsync(
    string? legacyStatus,
    CancellationToken cancellationToken)
    {
        var normalized = (legacyStatus ?? string.Empty).Trim().ToLowerInvariant();

        var statusName = normalized switch
        {
            "open" or "åben" or "tildelt" => "Tildelt",
            "won" or "vundet" or "realised" or "realiseret" => "Vundet",
            "lost" or "tabt" => "Tabt",
            "ongoing" or "pågår" or "paagar" => "Pågår",
            "removed" or "fjern" or "fjernet" => "Fjern",
            _ => "Tildelt"
        };

        return await _dbContext.OfferStatuses
            .Where(x => x.Name == statusName && x.IsActive)
            .Select(x => (int?)x.OfferStatusId)
            .FirstOrDefaultAsync(cancellationToken);
    }
    private async Task<ResourcePlan> ResolveResourcePlanAsync(int employeeId, int scenarioId, CancellationToken cancellationToken)
    {
        var existing = await _dbContext.ResourcePlans
            .FirstOrDefaultAsync(p => p.EmployeeId == employeeId && p.ScenarioId == scenarioId, cancellationToken);

        if (existing is not null)
        {
            return existing;
        }

        var plan = new ResourcePlan
        {
            EmployeeId = employeeId,
            ScenarioId = scenarioId,
            StartYear = DateTime.UtcNow.Year,
            StartMonth = 1,
            VisibleMonths = 12,
            IsActive = true,
            CreatedBy = "legacy-import",
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.ResourcePlans.Add(plan);
        return plan;
    }

    private async Task<ResourcePlan> ResolveResourcePlanForVirtualResourceAsync(
        int virtualResourceId, int scenarioId, CancellationToken cancellationToken)
    {
        var existing = await _dbContext.ResourcePlans
            .FirstOrDefaultAsync(p => p.VirtualResourceId == virtualResourceId && p.ScenarioId == scenarioId, cancellationToken);

        if (existing is not null)
        {
            return existing;
        }

        var plan = new ResourcePlan
        {
            VirtualResourceId = virtualResourceId,
            ScenarioId = scenarioId,
            StartYear = DateTime.UtcNow.Year,
            StartMonth = 1,
            VisibleMonths = 12,
            IsActive = true,
            CreatedBy = "legacy-import",
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.ResourcePlans.Add(plan);
        return plan;
    }

    private static bool TryParseMonthYear(string? monthYear, out int year, out int month)
    {
        year = 0;
        month = 0;

        if (string.IsNullOrWhiteSpace(monthYear))
        {
            return false;
        }

        // Expected format: "2026-02"
        var parts = monthYear.Split('-');
        if (parts.Length != 2)
        {
            return false;
        }

        return int.TryParse(parts[0], out year) && int.TryParse(parts[1], out month);
    }

    private static List<DateOnly> GetBusinessDaysForMonth(int year, int month)
    {
        var result = new List<DateOnly>();
        var firstDay = new DateOnly(year, month, 1);
        var lastDay = new DateOnly(year, month, DateTime.DaysInMonth(year, month));

        for (var day = firstDay; day <= lastDay; day = day.AddDays(1))
        {
            if (day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            {
                continue;
            }

            result.Add(day);
        }

        return result;
    }

    private static IReadOnlyList<decimal> DistributeHoursAcrossBusinessDays(decimal totalHours, int dayCount)
    {
        if (dayCount <= 0)
        {
            return [];
        }

        if (totalHours < 0)
        {
            throw new InvalidOperationException("Legacy import cannot distribute negative hours across day entries.");
        }

        var distributed = new decimal[dayCount];
        var totalCents = (int)Math.Round(totalHours * 100m, 0, MidpointRounding.AwayFromZero);
        var baseCents = totalCents / dayCount;
        var remainingCents = totalCents % dayCount;

        for (var i = 0; i < dayCount; i++)
        {
            var centsForDay = baseCents;

            if (remainingCents > 0)
            {
                centsForDay++;
                remainingCents--;
            }

            distributed[i] = centsForDay / 100m;
        }

        return distributed;
    }

    // Covers every planning-target CHECK constraint that can reject a bad TargetType/FK
    // combination, not just the one this was first written against — a mismatch between
    // CK_core_planning_targets_target_type and its sibling CK_core_planning_targets_type_matches_source
    // (and CK_core_planning_targets_exactly_one_source) is exactly how this failed last time.
    private static bool IsInvalidPlanningTargetTypeFailure(DbUpdateException exception)
    {
        return exception.InnerException is SqlException sqlException
            && sqlException.Message.Contains("CK_core_planning_targets_", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<int?> TryResolveSuffixedProjectNumberAsync(string code, CancellationToken cancellationToken)
    {
        var match = ProjectWithSuffixPattern.Match(code);

        if (!match.Success || !int.TryParse(match.Groups["projectNumber"].Value, out var projectNumber))
        {
            return null;
        }

        var projectExists = await _dbContext.Projects
            .AnyAsync(p => p.ProjectNumber == projectNumber, cancellationToken);

        return projectExists ? projectNumber : null;
    }

    private IReadOnlyList<LegacyInvalidPlanningTargetInfo> GetPendingPlanningTargetTypes()
    {
        return _dbContext.ChangeTracker
            .Entries<PlanningTarget>()
            .Where(x => x.State is EntityState.Added or EntityState.Modified)
            .Select(x => x.Entity)
            .Select(x => new LegacyInvalidPlanningTargetInfo
            {
                Code = x.Code,
                Name = x.Name,
                TargetType = x.TargetType,
                ExtProjectNumber = x.ExtProjectNumber,
                OfferId = x.OfferId,
                InternalPlanningCodeId = x.InternalPlanningCodeId
            })
            .ToList();
    }

    private void DetachPendingPlanningTargets()
    {
        foreach (var entry in _dbContext.ChangeTracker.Entries<PlanningTarget>()
                     .Where(x => x.State is EntityState.Added or EntityState.Modified))
        {
            entry.State = EntityState.Detached;
        }
    }

    private sealed class LegacyRessourceplanItem
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        public int? EmployeeId { get; set; }

        public int? VirtualResourceId { get; set; }

        [JsonPropertyName("project")]
        public string Project { get; set; } = string.Empty;

        [JsonPropertyName("monthYear")]
        public string MonthYear { get; set; } = string.Empty;

        [JsonPropertyName("hours")]
        public decimal Hours { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("createdAt")]
        public DateTime CreatedAt { get; set; }
    }
}

public sealed class LegacyImportRequest
{
    public int ScenarioId { get; set; }
}

public sealed class LegacyImportResult
{
    public string Message { get; set; } = string.Empty;
    public int Created { get; set; }
    public int Updated { get; set; }
    public int Skipped { get; set; }
    public IReadOnlyList<LegacyInvalidPlanningTargetInfo> InvalidTargetTypes { get; set; } = [];

    /// <summary>
    /// Numeric project codes that weren't found in the local e-conomic project mirror and were
    /// filed as internal planning codes instead. Run a project sync and re-import, or these
    /// hours will stay disconnected from the real project.
    /// </summary>
    public IReadOnlyList<string> UnsyncedProjectCodes { get; set; } = [];

    /// <summary>
    /// Initials from the workbook that didn't match any active ext.users email prefix, so all
    /// of that person's rows were skipped. This is the previously-silent cause of "missing" data.
    /// </summary>
    public IReadOnlyList<string> UnresolvedInitials { get; set; } = [];
}

public sealed class LegacyInvalidPlanningTargetInfo
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string TargetType { get; set; } = string.Empty;
    public int? ExtProjectNumber { get; set; }
    public int? OfferId { get; set; }
    public int? InternalPlanningCodeId { get; set; }
}
