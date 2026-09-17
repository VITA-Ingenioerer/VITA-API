using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Vita.Atlas.Application.DTOs;
using Vita.Atlas.Application.Interfaces;
using Vita.Atlas.Infrastructure.Data;
using Vita.Atlas.Infrastructure.Data.Entities;

namespace Vita.Atlas.Api.Controllers;

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

    private readonly AtlasDbContext _dbContext;
    private readonly ISyncRunService _syncRunService;

    public PlanningMetadataImportController(AtlasDbContext dbContext, ISyncRunService syncRunService)
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
        CancellationToken cancellationToken,
        bool dryRun = false)
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

            var probability = ParseProbabilityPercent(item.Sandsynlighed);
            if (probability is < 0 or > 100)
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
                    // A narrow top-up, not the full offer import.
                    //
                    // POST /api/offers/import owns the whole offer record — its ApplyImport
                    // assigns every column unconditionally, so a sheet that omits one blanks it.
                    // The Projekter sheet carries five offer-relevant columns out of roughly
                    // twenty, so routing these rows through that path would wipe status, PQ
                    // dates, expected start/end, project type and notes off all 113 existing
                    // offers. Only the columns this sheet actually owns are written here.
                    var offerState = await UpsertOfferMetadataAsync(item, projectCode, probability, result, index + 1, cancellationToken);

                    if (offerState == EntityImportState.Created)
                    {
                        result.OffersCreated++;
                    }
                    else
                    {
                        result.OffersUpdated++;
                    }

                    continue;
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

        if (dryRun)
        {
            // Everything above ran for real — routing, validation, the upserts themselves — so
            // the counts and issues are exactly what a live run would produce. Only the write is
            // withheld, and the tracked changes are dropped so nothing leaks into a later save.
            _dbContext.ChangeTracker.Clear();

            await _syncRunService.CompleteRunAsync(
                runId,
                "success",
                rowsRead: items.Count,
                errorCount: result.Issues.Count,
                notes: "Dry run — nothing was written.",
                cancellationToken: cancellationToken);

            result.Message = "Dry run complete. Nothing was written.";
            result.DryRun = true;
            return Ok(result);
        }

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            // The whole run saves as one batch, so a constraint violation names neither the
            // spreadsheet row nor the column it came from. Report what the change tracker was
            // actually writing, otherwise the only thing to go on is a bare SqlException.
            var details = DescribeFailedEntries(ex);
            await _syncRunService.LogErrorAsync(
                runId, "internal", resourceName, "save", details, cancellationToken: cancellationToken);
            await _syncRunService.CompleteRunAsync(
                runId, "failed", errorCount: 1, notes: details, cancellationToken: cancellationToken);

            return BadRequest(new
            {
                message = "The import could not be saved. " + (ex.InnerException?.Message ?? ex.Message),
                details
            });
        }

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

    /// <summary>
    /// Imports the Projekter sheet from an uploaded "Ressourceplan - Back end" workbook.
    ///
    /// Separate from planning-metadata-excel because that one assumes a plain .xlsx whose first
    /// used row is the header. This workbook is an .xlsm whose Projekter headers sit on row 9,
    /// under a Fa/In/Fr/Sa legend, and whose Sandsynlighed column holds fractions rather than
    /// percentages. RessourceplanWorkbookFile handles all three; pointing the old endpoint at
    /// this file skipped all 650 rows as "Missing projektnr.".
    /// </summary>
    [HttpPost("ressourceplan-workbook-metadata")]
    [RequestSizeLimit(50 * 1024 * 1024)]
    public async Task<ActionResult<PlanningMetadataImportResult>> ImportRessourceplanWorkbookMetadata(
        IFormFile file,
        [FromQuery] string? target,
        [FromQuery] bool dryRun,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(new { message = "A workbook file is required." });
        }

        var extension = Path.GetExtension(file.FileName);

        if (!extension.Equals(".xlsm", StringComparison.OrdinalIgnoreCase) &&
            !extension.Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { message = "Only .xlsm and .xlsx files are supported." });
        }

        var runId = await _syncRunService.StartRunAsync(
            "internal", "ressourceplan-workbook-metadata", ResolveInitiatedBy(),
            notes: file.FileName, cancellationToken: cancellationToken);

        RessourceplanWorkbookFile.Contents contents;

        try
        {
            // Copied to memory first: ClosedXML seeks, and the upload stream may not support it.
            using var buffer = new MemoryStream();
            await file.CopyToAsync(buffer, cancellationToken);
            buffer.Position = 0;
            contents = RessourceplanWorkbookFile.Parse(buffer);
        }
        catch (Exception ex)
        {
            await _syncRunService.LogErrorAsync(
                runId, "internal", "ressourceplan-workbook-metadata", "parse-workbook", ex.Message,
                cancellationToken: cancellationToken);
            await _syncRunService.CompleteRunAsync(
                runId, "failed", errorCount: 1, notes: ex.Message, cancellationToken: cancellationToken);
            throw;
        }

        if (contents.ProjectRows.Count == 0)
        {
            var emptyMessage = contents.Warnings.Count > 0
                ? string.Join(" ", contents.Warnings)
                : $"No rows were found in the '{RessourceplanWorkbookFile.ProjectsWorksheetName}' worksheet.";

            await _syncRunService.CompleteRunAsync(
                runId, "failed", rowsRead: 0, notes: emptyMessage, cancellationToken: cancellationToken);

            return BadRequest(new { message = emptyMessage });
        }

        var uploadRequest = new PlanningMetadataImportUploadRequest { File = file, Target = target };

        var response = await ProcessItems(
            contents.ProjectRows, uploadRequest, runId, "ressourceplan-workbook-metadata", cancellationToken, dryRun);

        if (response.Result is OkObjectResult { Value: PlanningMetadataImportResult result })
        {
            result.HeaderRowNumber = contents.ProjectsHeaderRowNumber;
            result.Warnings = contents.Warnings;
        }

        return response;
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
                    Projektnr    = GetXlString(row, headers, "Projektnr.") ?? GetXlString(row, headers, "Projektnr") ?? GetXlString(row, headers, "Tilbudsnr."),
                    Projektnavn  = GetXlString(row, headers, "Projektnavn"),
                    Sandsynlighed = ParseXlJsonElement(GetXlString(row, headers, "Sandsynlighed")),
                    Kundenr      = ParseXlJsonElement(GetXlString(row, headers, "Kundenr.")),
                    Kundenavn    = GetXlString(row, headers, "Kundenavn") ?? GetXlString(row, headers, "Kunde"),
                    Pl           = GetXlString(row, headers, "PL") ?? GetXlString(row, headers, "Ansvarlig person"),
                    Projektejer  = GetXlString(row, headers, "Projektejer"),
                    Honorar      = ParseXlJsonElement(GetXlString(row, headers, "Honorar")),
                    Kode         = GetXlString(row, headers, "Kode (Fa/In/Fr/Sa)") ?? GetXlString(row, headers, "Kode"),
                    IndtastningerPaaProjektet = ParseXlJsonElement(GetXlString(row, headers, "Er der indtastninger på projektet?")),
                    Projektkompleksitet = GetXlString(row, headers, "Projektkompleksitet"),
                    SenestAendret = GetXlString(row, headers, "Senest ændret") ?? GetXlString(row, headers, "Sidst redigeret"),
                    Konstruktionsklasse = GetXlString(row, headers, "Konstruktionsklasse"),
                    Brandklasse  = GetXlString(row, headers, "Brandklasse"),
                    AnsvarligtKontor = GetXlString(row, headers, "Ansvarligt kontor"),
                    Bygherre     = GetXlString(row, headers, "Bygherre"),
                    Totalentreprenoer = GetXlString(row, headers, "Totalentreprenør"),
                    Arkitekt     = GetXlString(row, headers, "Arkitekt"),
                    OevrigtTeam  = GetXlString(row, headers, "Øvrigt team"),
                    ForventetStart = GetXlDateOrText(row, headers, "Forventet start"),
                    ForventetSlut = GetXlDateOrText(row, headers, "Forventet slut"),
                    Storrelse    = GetXlString(row, headers, "Størrelse"),
                    Relation     = GetXlString(row, headers, "Relation"),
                    DatoForAfleveringAfPq = GetXlDateOrText(row, headers, "Dato for aflevering af PQ"),
                    Status = GetXlString(row, headers, "Status")
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

    // Some source columns (Forventet start/slut, Dato for aflevering af PQ) are a mix of real
    // Excel dates and free-text ("Q4 2022", "3 juni PQ"). Emitting a real date cell as ISO here
    // gives the downstream parser one unambiguous format to try first, instead of depending on
    // the cell's display format matching what the parser expects.
    private static string? GetXlDateOrText(IXLRow row, IReadOnlyDictionary<string, int> headers, string header)
    {
        if (!headers.TryGetValue(header, out var col))
        {
            return null;
        }

        var cell = row.Cell(col);

        if (cell.DataType == XLDataType.DateTime)
        {
            return cell.GetDateTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        var value = cell.GetFormattedString().Trim();
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

    private async Task<EntityImportState> UpsertProjectMetadataAsync(
        PlanningMetadataImportItem item,
        int projectNumber,
        decimal? probability,
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

        // No percentage stated means certain — same default the offer side uses.
        var probabilityPercent = probability ?? 100m;

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

    /// <summary>
    /// Writes only what the Projekter sheet knows about an offer: title, probability, fee,
    /// customer and responsible initials. Everything else on core.offers is left exactly as it
    /// is, because this sheet is not the authority for it.
    /// </summary>
    private async Task<EntityImportState> UpsertOfferMetadataAsync(
        PlanningMetadataImportItem item,
        string offerNumber,
        decimal? probability,
        PlanningMetadataImportResult result,
        int rowNumber,
        CancellationToken cancellationToken)
    {
        var normalizedOfferNumber = offerNumber.Trim().ToUpperInvariant();

        var entity = await _dbContext.Offers
            .FirstOrDefaultAsync(x => x.OfferNumber == normalizedOfferNumber, cancellationToken);

        var state = entity is null ? EntityImportState.Created : EntityImportState.Updated;
        var now = DateTime.UtcNow;
        var title = Normalize(item.Projektnavn);

        if (entity is null)
        {
            entity = new Offer
            {
                OfferNumber = normalizedOfferNumber,
                // Title is required; the offer number is the only thing guaranteed present.
                Title = TrimToMaxLength(title, 255) ?? normalizedOfferNumber,
                IsActive = true,
                AddToPqCompetition = false,
                CreatedBy = ImportActor,
                CreatedAtUtc = now
            };

            _dbContext.Offers.Add(entity);
        }
        else
        {
            // A blank cell means "this sheet does not say", not "clear it".
            if (!string.IsNullOrWhiteSpace(title))
            {
                entity.Title = TrimToMaxLength(title, 255)!;
            }

            entity.UpdatedBy = ImportActor;
            entity.UpdatedAtUtc = now;
        }

        // Same reasoning: only overwrite the probability when the sheet states one. The full
        // offer import defaults a blank to 100%, but it owns the record — silently promoting a
        // 25% offer to certain because this sheet left the cell empty would be a real loss.
        if (probability.HasValue)
        {
            entity.ProbabilityPercent = probability.Value;
            entity.IsProbableCase = probability.Value < 100m;
        }

        var fee = ParseNullableDecimal(item.Honorar);
        if (fee.HasValue)
        {
            entity.FeeAmount = fee;
        }

        // Initials only stick if they name a real active user — the column also carries free
        // text that names nobody, and the offers importer applies the same rule.
        var initials = NormalizeUpper(item.Pl);
        if (!string.IsNullOrWhiteSpace(initials))
        {
            var isKnown = await _dbContext.Users
                .AnyAsync(u => u.IsActive && u.UserPrincipalName != null
                               && u.UserPrincipalName.StartsWith(initials + "@"), cancellationToken);

            if (isKnown)
            {
                entity.ResponsibleInitials = TrimToMaxLength(initials, 20);
            }
        }

        // Matched against existing customers only. Creating one per unmatched name would seed
        // core.customers with typos from a sheet that is not the customer authority.
        var customerName = Normalize(item.Kundenavn);
        if (!string.IsNullOrWhiteSpace(customerName))
        {
            var customerId = await _dbContext.Customers
                .Where(c => c.Name == customerName)
                .Select(c => (int?)c.CustomerId)
                .FirstOrDefaultAsync(cancellationToken);

            if (customerId.HasValue)
            {
                entity.CustomerId = customerId;
            }
            else
            {
                result.Issues.Add(new PlanningMetadataImportIssue
                {
                    Row = rowNumber,
                    Projektnr = offerNumber,
                    Reason = $"Customer '{customerName}' was not found, so the offer was imported without a customer link."
                });
            }
        }

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

    // Spells out which entities EF was writing and which of their properties it considered
    // changed — the two facts a constraint violation from a batched SaveChanges doesn't give you.
    private static string DescribeFailedEntries(DbUpdateException ex)
    {
        if (ex.Entries.Count == 0)
        {
            return "EF reported no entries for the failure: " + (ex.InnerException?.Message ?? ex.Message);
        }

        var parts = new List<string>();
        foreach (var entry in ex.Entries)
        {
            var key = entry.Metadata.FindPrimaryKey();
            var keyText = key is null
                ? "?"
                : string.Join(", ", key.Properties.Select(pk => $"{pk.Name}={entry.Property(pk.Name).CurrentValue}"));

            var changed = entry.Properties
                .Where(prop => entry.State == EntityState.Added || prop.IsModified)
                .Select(prop => $"{prop.Metadata.Name}={FormatValue(prop.CurrentValue)}")
                .ToList();

            parts.Add($"{entry.Entity.GetType().Name}[{keyText}] {entry.State}: "
                + (changed.Count == 0 ? "(no changed properties)" : string.Join(", ", changed)));
        }

        return string.Join(" | ", parts);
    }

    private static string FormatValue(object? value)
    {
        return value switch
        {
            null => "NULL",
            string text => text.Length > 60 ? text[..60] + "…" : text,
            _ => value.ToString() ?? "NULL"
        };
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

    // Returns null for a blank cell and preserves an explicit 0. The two are not the same: in the
    // source sheet 0% marks a lost/withdrawn case, so folding it into the "nothing stated" default
    // would flip dead offers to a certainty.
    private static decimal? ParseProbabilityPercent(JsonElement? value)
    {
        return ParseNullableDecimal(value);
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

    private static readonly Regex QuarterYearPattern =
        new(@"Q\s*(?<q>[1-4])\s*,?\s*(?<y>\d{4})", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex YearOnlyPattern = new(@"^\d{4}$", RegexOptions.Compiled);
    private static readonly Regex IsoDatePattern = new(@"^(?<y>\d{4})-(?<m>\d{2})-(?<d>\d{2})$", RegexOptions.Compiled);

    private static readonly HashSet<string> UnknownAreaTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "ukendt", "n/a", "-", "?", "fortrolig"
    };

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

    [JsonPropertyName("ansvarligt_kontor")]
    public string? AnsvarligtKontor { get; set; }

    [JsonPropertyName("bygherre")]
    public string? Bygherre { get; set; }

    [JsonPropertyName("totalentreprenoer")]
    public string? Totalentreprenoer { get; set; }

    [JsonPropertyName("arkitekt")]
    public string? Arkitekt { get; set; }

    [JsonPropertyName("oevrigt_team")]
    public string? OevrigtTeam { get; set; }

    [JsonPropertyName("forventet_start")]
    public string? ForventetStart { get; set; }

    [JsonPropertyName("forventet_slut")]
    public string? ForventetSlut { get; set; }

    [JsonPropertyName("stoerrelse")]
    public string? Storrelse { get; set; }

    [JsonPropertyName("relation")]
    public string? Relation { get; set; }

    [JsonPropertyName("dato_for_aflevering_af_pq")]
    public string? DatoForAfleveringAfPq { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }
}

public sealed class PlanningMetadataImportResult
{
    public string Message { get; set; } = string.Empty;
    public bool DryRun { get; set; }
    /// <summary>Which row of the Projekter sheet the headers were found on, so a mis-read is visible.</summary>
    public int? HeaderRowNumber { get; set; }
    public List<string> Warnings { get; set; } = [];
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
