using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Vita.Planning.Application.DTOs;
using Vita.Planning.Application.Interfaces;
using Vita.Planning.Infrastructure.Data;
using Vita.Planning.Infrastructure.Data.Entities;

namespace Vita.Planning.Api.Controllers;

[ApiController]
[Route("api/import")]
public sealed class PlanningMetadataImportController : ControllerBase
{
    private const string ImportActor = "planning-metadata-import";
    private static readonly Regex OfferNumberPattern = new(@"^T\d+[A-Z]?$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true
    };

    private readonly PlanningDbContext _dbContext;
    private readonly ISyncRunService _syncRunService;

    public PlanningMetadataImportController(PlanningDbContext dbContext, ISyncRunService syncRunService)
    {
        _dbContext = dbContext;
        _syncRunService = syncRunService;
    }

    private string ResolveInitiatedBy()
    {
        var caller = CallerInfo.FromClaimsPrincipal(User);
        return caller.UserId is { Length: > 0 } userId ? userId
            : caller.Email is { Length: > 0 } email ? email
            : caller.Name is { Length: > 0 } name ? name
            : ImportActor;
    }

    [HttpPost("planning-metadata-json")]
    public async Task<ActionResult<PlanningMetadataImportResult>> ImportJson(
        [FromForm] PlanningMetadataImportUploadRequest request,
        CancellationToken cancellationToken)
    {
        if (request.File is null || request.File.Length == 0)
        {
            return BadRequest(new { message = "A JSON file is required." });
        }

        List<PlanningMetadataImportItem> items;
        var runId = await _syncRunService.StartRunAsync(
            "internal", "planning-metadata-json", ResolveInitiatedBy(),
            notes: request.File.FileName, cancellationToken: cancellationToken);

        try
        {
            await using var stream = request.File.OpenReadStream();
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            items = ParseItems(document.RootElement);
        }
        catch (Exception ex)
        {
            await _syncRunService.LogErrorAsync(
                runId, "internal", "planning-metadata-json", "parse-json", ex.Message, cancellationToken: cancellationToken);
            await _syncRunService.CompleteRunAsync(runId, "failed", errorCount: 1, notes: ex.Message, cancellationToken: cancellationToken);
            throw;
        }

        if (items.Count == 0)
        {
            const string emptyMessage = "The uploaded JSON file did not contain any records.";
            await _syncRunService.CompleteRunAsync(runId, "failed", rowsRead: 0, notes: emptyMessage, cancellationToken: cancellationToken);
            return BadRequest(new { message = emptyMessage });
        }

        return await ProcessItems(items, request, runId, "planning-metadata-json", cancellationToken);
    }

    private async Task<ActionResult<PlanningMetadataImportResult>> ProcessItems(
        List<PlanningMetadataImportItem> items,
        PlanningMetadataImportUploadRequest request,
        long runId,
        string resourceName,
        CancellationToken cancellationToken)
    {
        var result = new PlanningMetadataImportResult();

        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];
            var projectCode = Normalize(item.Projektnr);

            if (string.IsNullOrWhiteSpace(projectCode))
            {
                result.Skipped++;
                result.Issues.Add(new PlanningMetadataImportIssue
                {
                    Row = index + 1,
                    Reason = "Missing projektnr."
                });
                continue;
            }

            var probability = NormalizeProbabilityPercent(item.Sandsynlighed);
            if (probability < 0 || probability > 100)
            {
                result.Skipped++;
                result.Issues.Add(new PlanningMetadataImportIssue
                {
                    Row = index + 1,
                    Projektnr = projectCode,
                    Reason = "Sandsynlighed must be between 0 and 100."
                });
                continue;
            }

            var target = ResolveTarget(request.Target, projectCode);

            switch (target)
            {
                case PlanningMetadataImportTarget.Offers:
                {
                    if (!IsValidOfferNumber(projectCode))
                    {
                        result.Skipped++;
                        result.Issues.Add(new PlanningMetadataImportIssue
                        {
                            Row = index + 1,
                            Projektnr = projectCode,
                            Reason = "Offer imports require projektnr to match the existing offer number format T12345."
                        });
                        continue;
                    }

                    var importState = await UpsertOfferAsync(item, projectCode, probability, cancellationToken);
                    if (importState == EntityImportState.Created)
                    {
                        result.OffersCreated++;
                    }
                    else
                    {
                        result.OffersUpdated++;
                    }

                    break;
                }
                case PlanningMetadataImportTarget.ProjectMetadata:
                {
                    if (!int.TryParse(projectCode, out var projectNumber))
                    {
                        result.Skipped++;
                        result.Issues.Add(new PlanningMetadataImportIssue
                        {
                            Row = index + 1,
                            Projektnr = projectCode,
                            Reason = "Project metadata imports require a numeric projektnr."
                        });
                        continue;
                    }

                    var projectExists = await _dbContext.Projects
                        .AnyAsync(x => x.ProjectNumber == projectNumber, cancellationToken);

                    if (!projectExists)
                    {
                        result.Skipped++;
                        result.Issues.Add(new PlanningMetadataImportIssue
                        {
                            Row = index + 1,
                            Projektnr = projectCode,
                            Reason = $"Project {projectNumber} does not exist in the system and cannot be imported yet. It may appear after the next ERP sync."
                        });
                        continue;
                    }

                    var importState = await UpsertProjectMetadataAsync(item, projectNumber, probability, cancellationToken);
                    if (importState == EntityImportState.Created)
                    {
                        result.ProjectMetadataCreated++;
                    }
                    else
                    {
                        result.ProjectMetadataUpdated++;
                    }

                    break;
                }
                case PlanningMetadataImportTarget.InternalPlanningCodes:
                {
                    var importState = await UpsertInternalPlanningCodeAsync(item, projectCode, cancellationToken);
                    if (importState == EntityImportState.Created)
                    {
                        result.InternalPlanningCodesCreated++;
                    }
                    else
                    {
                        result.InternalPlanningCodesUpdated++;
                    }

                    break;
                }
                default:
                    throw new InvalidOperationException("Unsupported import target.");
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        foreach (var issue in result.Issues)
        {
            await _syncRunService.LogErrorAsync(
                runId, "internal", resourceName, "row",
                issue.Reason, recordKey: $"row {issue.Row} ({issue.Projektnr})", cancellationToken: cancellationToken);
        }

        var totalWritten = result.OffersCreated + result.OffersUpdated
            + result.ProjectMetadataCreated + result.ProjectMetadataUpdated
            + result.InternalPlanningCodesCreated + result.InternalPlanningCodesUpdated;

        await _syncRunService.CompleteRunAsync(
            runId,
            result.Issues.Count == 0 ? "success" : "partial",
            rowsRead: items.Count,
            rowsInserted: totalWritten,
            errorCount: result.Issues.Count,
            cancellationToken: cancellationToken);

        result.Message = "Import complete.";
        return Ok(result);
    }

    [HttpPost("planning-metadata-excel")]
    [RequestSizeLimit(25 * 1024 * 1024)]
    public async Task<ActionResult<PlanningMetadataImportResult>> ImportExcel(
        IFormFile file,
        [FromQuery] string? target,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(new { message = "An Excel file is required." });
        }

        if (!Path.GetExtension(file.FileName).Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { message = "Only .xlsx files are supported." });
        }

        var runId = await _syncRunService.StartRunAsync(
            "internal", "planning-metadata-excel", ResolveInitiatedBy(),
            notes: file.FileName, cancellationToken: cancellationToken);

        List<PlanningMetadataImportItem> items;

        try
        {
            await using var stream = file.OpenReadStream();
            using var workbook = new XLWorkbook(stream);
            var worksheet = workbook.Worksheets.FirstOrDefault()
                ?? throw new InvalidOperationException("The workbook does not contain any worksheets.");

            var firstRow = worksheet.FirstRowUsed()
                ?? throw new InvalidOperationException("The worksheet does not contain a header row.");

            var lastCol = firstRow.LastCellUsed()?.Address.ColumnNumber ?? 0;
            var headers = Enumerable.Range(1, lastCol)
                .Select(c => new { Col = c, Header = worksheet.Cell(firstRow.RowNumber(), c).GetString().Trim() })
                .Where(x => !string.IsNullOrWhiteSpace(x.Header))
                .ToDictionary(x => x.Header, x => x.Col, StringComparer.OrdinalIgnoreCase);

            items = worksheet.RowsUsed()
                .Where(r => r.RowNumber() > firstRow.RowNumber())
                .Select(row => new PlanningMetadataImportItem
                {
                    Projektnr    = GetXlString(row, headers, "Projektnr.") ?? GetXlString(row, headers, "Projektnr"),
                    Projektnavn  = GetXlString(row, headers, "Projektnavn"),
                    Sandsynlighed = ParseXlJsonElement(GetXlString(row, headers, "Sandsynlighed")),
                    Kundenr      = ParseXlJsonElement(GetXlString(row, headers, "Kundenr.")),
                    Kundenavn    = GetXlString(row, headers, "Kundenavn"),
                    Pl           = GetXlString(row, headers, "PL"),
                    Projektejer  = GetXlString(row, headers, "Projektejer"),
                    Honorar      = ParseXlJsonElement(GetXlString(row, headers, "Honorar")),
                    Kode         = GetXlString(row, headers, "Kode (Fa/In/Fr/Sa)") ?? GetXlString(row, headers, "Kode"),
                    IndtastningerPaaProjektet = ParseXlJsonElement(GetXlString(row, headers, "Er der indtastninger på projektet?")),
                    Projektkompleksitet = GetXlString(row, headers, "Projektkompleksitet"),
                    SenestAendret = GetXlString(row, headers, "Senest ændret"),
                    Konstruktionsklasse = GetXlString(row, headers, "Konstruktionsklasse"),
                    Brandklasse  = GetXlString(row, headers, "Brandklasse")
                })
                .ToList();
        }
        catch (Exception ex)
        {
            await _syncRunService.LogErrorAsync(
                runId, "internal", "planning-metadata-excel", "parse-workbook", ex.Message, cancellationToken: cancellationToken);
            await _syncRunService.CompleteRunAsync(runId, "failed", errorCount: 1, notes: ex.Message, cancellationToken: cancellationToken);
            throw;
        }

        if (items.Count == 0)
        {
            const string emptyMessage = "The uploaded Excel file did not contain any records.";
            await _syncRunService.CompleteRunAsync(runId, "failed", rowsRead: 0, notes: emptyMessage, cancellationToken: cancellationToken);
            return BadRequest(new { message = emptyMessage });
        }

        var uploadRequest = new PlanningMetadataImportUploadRequest { Target = target };
        return await ProcessItems(items, uploadRequest, runId, "planning-metadata-excel", cancellationToken);
    }

    private static string? GetXlString(IXLRow row, IReadOnlyDictionary<string, int> headers, string header)
    {
        if (!headers.TryGetValue(header, out var col))
        {
            return null;
        }

        var value = row.Cell(col).GetFormattedString().Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static JsonElement? ParseXlJsonElement(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        // Try as number first, then fall back to string
        var normalized = value.Trim().Replace(',', '.');
        if (decimal.TryParse(normalized, NumberStyles.Any, CultureInfo.InvariantCulture, out _))
        {
            return JsonSerializer.Deserialize<JsonElement>($"{normalized}");
        }

        var escaped = JsonSerializer.Serialize(value);
        return JsonSerializer.Deserialize<JsonElement>(escaped);
    }

    private static List<PlanningMetadataImportItem> ParseItems(JsonElement root)
    {
        return root.ValueKind switch
        {
            JsonValueKind.Array => JsonSerializer.Deserialize<List<PlanningMetadataImportItem>>(root.GetRawText(), JsonOptions) ?? [],
            JsonValueKind.Object =>
            [
                JsonSerializer.Deserialize<PlanningMetadataImportItem>(root.GetRawText(), JsonOptions)
                ?? new PlanningMetadataImportItem()
            ],
            _ => []
        };
    }

    private async Task<EntityImportState> UpsertOfferAsync(
        PlanningMetadataImportItem item,
        string projectCode,
        decimal probabilityPercent,
        CancellationToken cancellationToken)
    {
        var offerNumber = projectCode.Trim().ToUpperInvariant();
        var entity = await _dbContext.Offers
            .FirstOrDefaultAsync(x => x.OfferNumber == offerNumber, cancellationToken);

        var state = entity is null ? EntityImportState.Created : EntityImportState.Updated;
        var now = DateTime.UtcNow;

        if (entity is null)
        {
            entity = new Offer
            {
                OfferNumber = offerNumber,
                OfferStatusId = await ResolveLegacyOfferStatusIdAsync("Imported", cancellationToken),
                IsActive = true,
                CreatedBy = ImportActor,
                CreatedAtUtc = now
            };

            _dbContext.Offers.Add(entity);
        }
        else
        {
            entity.UpdatedBy = ImportActor;
            entity.UpdatedAtUtc = now;
        }

        entity.Title = TrimToMaxLength(Normalize(item.Projektnavn) ?? offerNumber, 255) ?? offerNumber;
        entity.ResponsibleInitials = TrimToMaxLength(NormalizeUpper(item.Pl), 20);

        entity.FeeAmount = ParseNullableDecimal(item.Honorar);
        entity.SizeDescription = TrimToMaxLength(Normalize(item.Projektkompleksitet), 255);
        entity.Notes = MergeText(entity.Notes, BuildOfferNotes(item), 1000);
        entity.IsActive = true;

        if (entity.OfferStatusId is null)
        {
            entity.OfferStatusId = await ResolveLegacyOfferStatusIdAsync("Open", cancellationToken);
        }

        return state;
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

    private async Task<EntityImportState> UpsertProjectMetadataAsync(
        PlanningMetadataImportItem item,
        int projectNumber,
        decimal probabilityPercent,
        CancellationToken cancellationToken)
    {
        var entity = await _dbContext.ProjectMetadata
            .FirstOrDefaultAsync(x => x.ProjectNumber == projectNumber, cancellationToken);

        var state = entity is null ? EntityImportState.Created : EntityImportState.Updated;
        var now = DateTime.UtcNow;
        var normalizedCode = NormalizeUpper(item.Kode);

        if (entity is null)
        {
            entity = new ProjectMetadata
            {
                ProjectNumber = projectNumber,
                CreatedAtUtc = now
            };

            _dbContext.ProjectMetadata.Add(entity);
        }
        else
        {
            entity.UpdatedAtUtc = now;
        }

        entity.PlanningCategory = TrimToMaxLength(normalizedCode, 100);
        entity.DisciplineOwner = TrimToMaxLength(NormalizeUpper(item.Pl), 100);
        entity.ProbabilityPercent = probabilityPercent;
        entity.DefaultDescription = MergeText(entity.DefaultDescription, BuildProjectMetadataDescription(item), int.MaxValue);
        entity.PlanningGroup = TrimToMaxLength(Normalize(item.Kundenavn), 100);
        entity.BudgetRevenue = ParseNullableDecimal(item.Honorar);
        entity.LastPlanningReviewBy = TrimToMaxLength(NormalizeUpper(item.Projektejer), 255);
        entity.IsInternal = string.Equals(normalizedCode, "IN", StringComparison.OrdinalIgnoreCase);
        entity.IsAbsence = string.Equals(normalizedCode, "FR", StringComparison.OrdinalIgnoreCase);
        entity.IsBillableForPlanning = string.Equals(normalizedCode, "FA", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalizedCode, "SA", StringComparison.OrdinalIgnoreCase);
        entity.IsProbableCase = probabilityPercent < 100m;

        return state;
    }

    private async Task<EntityImportState> UpsertInternalPlanningCodeAsync(
        PlanningMetadataImportItem item,
        string projectCode,
        CancellationToken cancellationToken)
    {
        var normalizedProjectCode = projectCode.Trim().ToUpperInvariant();
        var entity = await _dbContext.InternalPlanningCodes
            .FirstOrDefaultAsync(x => x.Code == normalizedProjectCode, cancellationToken);

        var state = entity is null ? EntityImportState.Created : EntityImportState.Updated;
        var now = DateTime.UtcNow;
        var normalizedCode = NormalizeUpper(item.Kode);
        var category = ResolveInternalPlanningCategory(normalizedCode);
        var isAbsence = string.Equals(normalizedCode, "FR", StringComparison.OrdinalIgnoreCase);
        var isBillable = string.Equals(normalizedCode, "FA", StringComparison.OrdinalIgnoreCase);

        if (entity is null)
        {
            entity = new InternalPlanningCode
            {
                Code = normalizedProjectCode,
                CreatedBy = ImportActor,
                CreatedAtUtc = now
            };

            _dbContext.InternalPlanningCodes.Add(entity);
        }
        else
        {
            entity.UpdatedBy = ImportActor;
            entity.UpdatedAtUtc = now;
        }

        entity.Name = TrimToMaxLength(Normalize(item.Projektnavn) ?? normalizedProjectCode, 150) ?? normalizedProjectCode;
        entity.Category = category;
        entity.DefaultDescription = MergeText(entity.DefaultDescription, BuildInternalPlanningCodeDescription(item), 255);
        entity.IsActive = true;
        entity.IsPlannable = true;
        entity.IsAbsence = isAbsence;
        entity.IsInternal = true;
        entity.IsBillable = isBillable;

        return state;
    }

    private static PlanningMetadataImportTarget ResolveTarget(string? target, string projectCode)
    {
        var normalizedTarget = target?.Trim().ToLowerInvariant();

        return normalizedTarget switch
        {
            "offer" or "offers" => PlanningMetadataImportTarget.Offers,
            "project" or "projects" or "projectmetadata" or "project-metadata" => PlanningMetadataImportTarget.ProjectMetadata,
            "internal" or "internals" or "internalproject" or "internal-project" or "internalprojects" or "internal-projects" => PlanningMetadataImportTarget.InternalPlanningCodes,
            _ => int.TryParse(projectCode, out _)
                ? PlanningMetadataImportTarget.ProjectMetadata
                : IsValidOfferNumber(projectCode)
                    ? PlanningMetadataImportTarget.Offers
                    : PlanningMetadataImportTarget.InternalPlanningCodes
        };
    }

    private static bool IsValidOfferNumber(string projectCode)
    {
        return OfferNumberPattern.IsMatch(projectCode);
    }

    private static decimal NormalizeProbabilityPercent(JsonElement? value)
    {
        var parsed = ParseNullableDecimal(value);
        if (!parsed.HasValue || parsed.Value == 0)
        {
            return 100m;
        }

        return parsed.Value;
    }

    private static string? BuildOfferNotes(PlanningMetadataImportItem item)
    {
        var values = new List<string>();

        AddLine(values, "Kundenr", GetScalarText(item.Kundenr));
        AddLine(values, "Projektejer", NormalizeUpper(item.Projektejer));
        AddLine(values, "Kode", NormalizeUpper(item.Kode));
        AddLine(values, "Indtastninger på projektet", GetScalarText(item.IndtastningerPaaProjektet));
        AddLine(values, "Senest ændret", Normalize(item.SenestAendret));
        AddLine(values, "Konstruktionsklasse", Normalize(item.Konstruktionsklasse));
        AddLine(values, "Brandklasse", Normalize(item.Brandklasse));

        return values.Count == 0 ? null : string.Join(Environment.NewLine, values);
    }

    private static string? BuildInternalPlanningCodeDescription(PlanningMetadataImportItem item)
    {
        var values = new List<string>();

        AddLine(values, "Kundenavn", Normalize(item.Kundenavn));
        AddLine(values, "PL", NormalizeUpper(item.Pl));
        AddLine(values, "Projektejer", NormalizeUpper(item.Projektejer));
        AddLine(values, "Honorar", GetScalarText(item.Honorar));
        AddLine(values, "Projektkompleksitet", Normalize(item.Projektkompleksitet));
        AddLine(values, "Senest ændret", Normalize(item.SenestAendret));

        return values.Count == 0 ? null : string.Join(Environment.NewLine, values);
    }

    private static string ResolveInternalPlanningCategory(string? code)
    {
        return code switch
        {
            "IN" => "Internal",
            "FR" => "Absence",
            "SA" => "Sales",
            "FA" => "SmallJobs",
            _ => "Other"
        };
    }

    private static string? BuildProjectMetadataDescription(PlanningMetadataImportItem item)
    {
        var values = new List<string>();

        AddLine(values, "Projektnavn", Normalize(item.Projektnavn));
        AddLine(values, "Kundenr", GetScalarText(item.Kundenr));
        AddLine(values, "Projektejer", NormalizeUpper(item.Projektejer));
        AddLine(values, "Indtastninger på projektet", GetScalarText(item.IndtastningerPaaProjektet));
        AddLine(values, "Projektkompleksitet", Normalize(item.Projektkompleksitet));
        AddLine(values, "Senest ændret", Normalize(item.SenestAendret));
        AddLine(values, "Konstruktionsklasse", Normalize(item.Konstruktionsklasse));
        AddLine(values, "Brandklasse", Normalize(item.Brandklasse));

        return values.Count == 0 ? null : string.Join(Environment.NewLine, values);
    }

    private static void AddLine(List<string> values, string label, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            values.Add($"{label}: {value.Trim()}");
        }
    }

    private static string? MergeText(string? existing, string? appended, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(appended))
        {
            return existing;
        }

        if (string.IsNullOrWhiteSpace(existing))
        {
            return TrimToMaxLength(appended.Trim(), maxLength);
        }

        if (existing.Contains(appended, StringComparison.Ordinal))
        {
            return TrimToMaxLength(existing, maxLength);
        }

        return TrimToMaxLength($"{existing.Trim()}{Environment.NewLine}{Environment.NewLine}{appended.Trim()}", maxLength);
    }

    private static string? TrimToMaxLength(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

    private static string? Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string? NormalizeUpper(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();
    }

    private static decimal? ParseNullableDecimal(JsonElement? value)
    {
        if (!value.HasValue)
        {
            return null;
        }

        return value.Value.ValueKind switch
        {
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            JsonValueKind.Number when value.Value.TryGetDecimal(out var numericValue) => numericValue,
            JsonValueKind.String => TryParseFlexibleDecimal(value.Value.GetString(), out var parsed) ? parsed : null,
            _ => TryParseFlexibleDecimal(value.Value.ToString(), out var fallback) ? fallback : null
        };
    }

    private static string? GetScalarText(JsonElement? value)
    {
        if (!value.HasValue)
        {
            return null;
        }

        return value.Value.ValueKind switch
        {
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            JsonValueKind.String => Normalize(value.Value.GetString()),
            JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => value.Value.ToString(),
            _ => Normalize(value.Value.GetRawText())
        };
    }

    private static bool TryParseFlexibleDecimal(string? value, out decimal result)
    {
        result = default;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value.Trim().Replace("%", string.Empty, StringComparison.Ordinal).Replace(" ", string.Empty, StringComparison.Ordinal);

        if (normalized.Count(c => c == '.') > 1 && !normalized.Contains(','))
        {
            normalized = normalized.Replace(".", string.Empty, StringComparison.Ordinal);
        }
        else if (normalized.Contains('.') && normalized.Contains(','))
        {
            normalized = normalized.Replace(".", string.Empty, StringComparison.Ordinal).Replace(',', '.');
        }
        else if (normalized.Contains(','))
        {
            normalized = normalized.Replace(',', '.');
        }

        return decimal.TryParse(normalized, NumberStyles.Number | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out result);
    }

    private enum PlanningMetadataImportTarget
    {
        Offers,
        ProjectMetadata,
        InternalPlanningCodes
    }

    private enum EntityImportState
    {
        Created,
        Updated
    }
}

public sealed class PlanningMetadataImportUploadRequest
{
    public IFormFile File { get; set; } = default!;
    public string? Target { get; set; }
}

public sealed class PlanningMetadataImportItem
{
    [JsonPropertyName("projektnr")]
    public string? Projektnr { get; set; }

    [JsonPropertyName("projektnavn")]
    public string? Projektnavn { get; set; }

    [JsonPropertyName("sandsynlighed")]
    public JsonElement? Sandsynlighed { get; set; }

    [JsonPropertyName("kundenr")]
    public JsonElement? Kundenr { get; set; }

    [JsonPropertyName("kundenavn")]
    public string? Kundenavn { get; set; }

    [JsonPropertyName("pl")]
    public string? Pl { get; set; }

    [JsonPropertyName("projektejer")]
    public string? Projektejer { get; set; }

    [JsonPropertyName("honorar")]
    public JsonElement? Honorar { get; set; }

    [JsonPropertyName("kode")]
    public string? Kode { get; set; }

    [JsonPropertyName("indtastninger_paa_projektet")]
    public JsonElement? IndtastningerPaaProjektet { get; set; }

    [JsonPropertyName("projektkompleksitet")]
    public string? Projektkompleksitet { get; set; }

    [JsonPropertyName("senest_aendret")]
    public string? SenestAendret { get; set; }

    [JsonPropertyName("konstruktionsklasse")]
    public string? Konstruktionsklasse { get; set; }

    [JsonPropertyName("brandklasse")]
    public string? Brandklasse { get; set; }
}

public sealed class PlanningMetadataImportResult
{
    public string Message { get; set; } = string.Empty;
    public int OffersCreated { get; set; }
    public int OffersUpdated { get; set; }
    public int ProjectMetadataCreated { get; set; }
    public int ProjectMetadataUpdated { get; set; }
    public int InternalPlanningCodesCreated { get; set; }
    public int InternalPlanningCodesUpdated { get; set; }
    public int Skipped { get; set; }
    public List<PlanningMetadataImportIssue> Issues { get; set; } = [];
}

public sealed class PlanningMetadataImportIssue
{
    public int Row { get; set; }
    public string? Projektnr { get; set; }
    public string Reason { get; set; } = string.Empty;
}
