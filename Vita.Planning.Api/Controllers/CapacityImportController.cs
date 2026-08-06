using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Vita.Planning.Application.DTOs;
using Vita.Planning.Application.Interfaces;
using Vita.Planning.Infrastructure.Data;
using Vita.Planning.Infrastructure.Data.Entities;
using Vita.Planning.Infrastructure.Services;

namespace Vita.Planning.Api.Controllers;

[ApiController]
[Route("api/import")]
public sealed class CapacityImportController : ControllerBase
{
    private readonly PlanningDbContext _dbContext;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly CapacityDefaultsSettings _capacityDefaults;
    private readonly ISyncRunService _syncRunService;

    public CapacityImportController(
        PlanningDbContext dbContext,
        IHttpClientFactory httpClientFactory,
        IOptions<CapacityDefaultsSettings> capacityDefaults,
        ISyncRunService syncRunService)
    {
        _dbContext = dbContext;
        _httpClientFactory = httpClientFactory;
        _capacityDefaults = capacityDefaults.Value;
        _syncRunService = syncRunService;
    }

    private string ResolveInitiatedBy()
    {
        var caller = CallerInfo.FromClaimsPrincipal(User);
        return caller.UserId is { Length: > 0 } userId ? userId
            : caller.Email is { Length: > 0 } email ? email
            : caller.Name is { Length: > 0 } name ? name
            : "capacity-import";
    }

    /// <summary>
    /// Fetches Danish public holidays from Nager.Date for the given years and upserts
    /// them into core.public_holiday_calendar.
    /// </summary>
    [HttpPost("danish-public-holidays")]
    public async Task<ActionResult<HolidayImportResult>> ImportHolidays(
        [FromBody] HolidayImportRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Years is null || request.Years.Count == 0)
        {
            return BadRequest(new { message = "At least one year must be provided." });
        }

        var runId = await _syncRunService.StartRunAsync(
            "NagerDate", "danish-public-holidays", ResolveInitiatedBy(), cancellationToken: cancellationToken);

        var client = _httpClientFactory.CreateClient("NagerDate");

        int created = 0;
        int skipped = 0;

        foreach (var year in request.Years)
        {
            List<NagerHolidayItem>? holidays;
            try
            {
                holidays = await client.GetFromJsonAsync<List<NagerHolidayItem>>(
                    $"api/v3/PublicHolidays/{year}/DK", cancellationToken);
            }
            catch (Exception ex)
            {
                await _syncRunService.LogErrorAsync(
                    runId, "NagerDate", "danish-public-holidays", "fetch", ex.Message, recordKey: year.ToString(), cancellationToken: cancellationToken);
                await _syncRunService.CompleteRunAsync(
                    runId, "failed", rowsInserted: created, errorCount: 1, notes: $"Failed fetching {year}", cancellationToken: cancellationToken);
                return StatusCode(502, new { message = $"Failed to fetch holidays for {year}: {ex.Message}" });
            }

            if (holidays is null)
            {
                continue;
            }

            foreach (var holiday in holidays)
            {
                if (!DateOnly.TryParse(holiday.Date, out var date))
                {
                    skipped++;
                    continue;
                }

                var existing = await _dbContext.PublicHolidayCalendars
                    .FirstOrDefaultAsync(h => h.CountryCode == "DK" && h.HolidayDate == date, cancellationToken);

                if (existing is not null)
                {
                    skipped++;
                    continue;
                }

                _dbContext.PublicHolidayCalendars.Add(new PublicHolidayCalendar
                {
                    CountryCode = "DK",
                    HolidayDate = date,
                    HolidayName = holiday.LocalName ?? holiday.Name,
                    IsPublicHoliday = true,
                    IsHalfDay = false,
                    HoursReduction = null,
                    CreatedAtUtc = DateTime.UtcNow
                });

                created++;
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _syncRunService.CompleteRunAsync(
            runId, "success", rowsRead: created + skipped, rowsInserted: created, cancellationToken: cancellationToken);

        return Ok(new HolidayImportResult
        {
            Message = "Holiday import complete.",
            Created = created,
            Skipped = skipped
        });
    }

    /// <summary>
    /// Ensures every active employee has at least one capacity profile.
    /// Creates a default 37 h/week open-ended profile for those that don't have one.
    /// </summary>
    [HttpPost("employee-capacity-profiles")]
    public async Task<ActionResult<CapacityProfileResult>> EnsureProfiles(CancellationToken cancellationToken)
    {
        var runId = await _syncRunService.StartRunAsync(
            "internal", "employee-capacity-profiles", ResolveInitiatedBy(), cancellationToken: cancellationToken);

        var employees = await _dbContext.Users
            .AsNoTracking()
            .Where(u => u.IsActive)
            .Select(u => u.EmployeeId)
            .ToListAsync(cancellationToken);

        var existingEmployeeIds = await _dbContext.EmployeeCapacityProfiles
            .AsNoTracking()
            .Select(p => p.EmployeeId)
            .Distinct()
            .ToHashSetAsync(cancellationToken);

        int created = 0;
        int skipped = 0;

        foreach (var employeeId in employees)
        {
            if (existingEmployeeIds.Contains(employeeId))
            {
                skipped++;
                continue;
            }

            _dbContext.EmployeeCapacityProfiles.Add(new EmployeeCapacityProfile
            {
                EmployeeId = employeeId,
                EffectiveFrom = new DateOnly(2020, 1, 1),
                EffectiveTo = null,
                DefaultWeeklyHours = _capacityDefaults.BaselineWeeklyHours,
                IsActive = true,
                CreatedBy = "capacity-import",
                CreatedAtUtc = DateTime.UtcNow
            });

            created++;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _syncRunService.CompleteRunAsync(
            runId, "success", rowsRead: employees.Count, rowsInserted: created, cancellationToken: cancellationToken);

        return Ok(new CapacityProfileResult
        {
            Message = "Profile ensure complete.",
            Created = created,
            Skipped = skipped
        });
    }

    /// <summary>
    /// Imports per-weekday capacity hours from an uploaded .xlsx sheet (columns: e-conomic Id,
    /// Mandag..Fredag). For each matched employee, updates their currently-active capacity
    /// profile in place (creating one if none exists yet). Unmatched e-conomic Ids are skipped
    /// and reported, not treated as a failure.
    /// </summary>
    [HttpPost("employee-capacity-profile-hours-excel")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<ActionResult<CapacityProfileHoursImportResult>> ImportProfileHoursExcel(
        IFormFile file,
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

        var result = new CapacityProfileHoursImportResult();
        var now = DateTime.UtcNow;
        var runId = await _syncRunService.StartRunAsync(
            "internal", "employee-capacity-profile-hours-excel", ResolveInitiatedBy(),
            notes: file.FileName, cancellationToken: cancellationToken);

        await using var stream = file.OpenReadStream();
        using var workbook = new ClosedXML.Excel.XLWorkbook(stream);

        ClosedXML.Excel.IXLWorksheet worksheet;
        ClosedXML.Excel.IXLRow headerRow;
        Dictionary<string, int> headers;
        try
        {
            worksheet = workbook.Worksheets.FirstOrDefault()
                ?? throw new InvalidOperationException("The workbook does not contain any worksheets.");

            headerRow = worksheet.FirstRowUsed()
                ?? throw new InvalidOperationException("The worksheet does not contain a header row.");

            var lastCol = headerRow.LastCellUsed()?.Address.ColumnNumber ?? 0;
            headers = Enumerable.Range(1, lastCol)
                .Select(c => new { Col = c, Header = worksheet.Cell(headerRow.RowNumber(), c).GetString().Trim() })
                .Where(x => !string.IsNullOrWhiteSpace(x.Header))
                .ToDictionary(x => x.Header, x => x.Col, StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            await _syncRunService.LogErrorAsync(
                runId, "internal", "employee-capacity-profile-hours-excel", "parse-workbook", ex.Message, cancellationToken: cancellationToken);
            await _syncRunService.CompleteRunAsync(runId, "failed", errorCount: 1, notes: ex.Message, cancellationToken: cancellationToken);
            throw;
        }

        var economicIdCol = FindHeaderColumn(headers, "e-conomic Id", "e-conomic id", "economic id", "economicid");
        var mondayCol = FindHeaderColumn(headers, "Mandag");
        var tuesdayCol = FindHeaderColumn(headers, "Tirsdag");
        var wednesdayCol = FindHeaderColumn(headers, "Onsdag");
        var thursdayCol = FindHeaderColumn(headers, "Torsdag");
        var fridayCol = FindHeaderColumn(headers, "Fredag");

        if (economicIdCol is null || mondayCol is null || tuesdayCol is null ||
            wednesdayCol is null || thursdayCol is null || fridayCol is null)
        {
            const string missingColumnsMessage = "The worksheet must contain columns: e-conomic Id, Mandag, Tirsdag, Onsdag, Torsdag, Fredag.";
            await _syncRunService.LogErrorAsync(
                runId, "internal", "employee-capacity-profile-hours-excel", "validate-columns", missingColumnsMessage, cancellationToken: cancellationToken);
            await _syncRunService.CompleteRunAsync(runId, "failed", errorCount: 1, notes: missingColumnsMessage, cancellationToken: cancellationToken);
            return BadRequest(new { message = missingColumnsMessage });
        }

        var dataRows = worksheet.RowsUsed().Where(r => r.RowNumber() > headerRow.RowNumber()).ToList();

        foreach (var row in dataRows)
        {
            var rowNumber = row.RowNumber();
            var economicIdText = row.Cell(economicIdCol.Value).GetFormattedString().Trim();

            if (string.IsNullOrWhiteSpace(economicIdText))
            {
                continue; // blank row
            }

            if (!int.TryParse(economicIdText, out var employeeId))
            {
                result.Skipped++;
                result.Issues.Add(new CapacityProfileHoursImportIssue
                {
                    Row = rowNumber,
                    EconomicId = economicIdText,
                    Reason = "e-conomic Id is not a valid number."
                });
                continue;
            }

            var employeeExists = await _dbContext.Users
                .AsNoTracking()
                .AnyAsync(u => u.EmployeeId == employeeId, cancellationToken);

            if (!employeeExists)
            {
                result.Skipped++;
                result.Issues.Add(new CapacityProfileHoursImportIssue
                {
                    Row = rowNumber,
                    EconomicId = economicIdText,
                    Reason = "No matching employee found for this e-conomic Id."
                });
                continue;
            }

            var monday = GetXlHours(row, mondayCol.Value);
            var tuesday = GetXlHours(row, tuesdayCol.Value);
            var wednesday = GetXlHours(row, wednesdayCol.Value);
            var thursday = GetXlHours(row, thursdayCol.Value);
            var friday = GetXlHours(row, fridayCol.Value);

            var profile = await _dbContext.EmployeeCapacityProfiles
                .Where(p => p.EmployeeId == employeeId && p.IsActive && p.EffectiveTo == null)
                .OrderByDescending(p => p.EffectiveFrom)
                .FirstOrDefaultAsync(cancellationToken);

            if (profile is null)
            {
                profile = new EmployeeCapacityProfile
                {
                    EmployeeId = employeeId,
                    EffectiveFrom = new DateOnly(2020, 1, 1),
                    EffectiveTo = null,
                    IsActive = true,
                    CreatedBy = "capacity-profile-hours-import",
                    CreatedAtUtc = now
                };

                _dbContext.EmployeeCapacityProfiles.Add(profile);
                result.Created++;
            }
            else
            {
                profile.UpdatedBy = "capacity-profile-hours-import";
                profile.UpdatedAtUtc = now;
                result.Updated++;
            }

            profile.MondayHours = monday;
            profile.TuesdayHours = tuesday;
            profile.WednesdayHours = wednesday;
            profile.ThursdayHours = thursday;
            profile.FridayHours = friday;
            profile.SaturdayHours = 0m;
            profile.SundayHours = 0m;
            profile.DefaultWeeklyHours = Math.Round(monday + tuesday + wednesday + thursday + friday, 2);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        foreach (var issue in result.Issues)
        {
            await _syncRunService.LogErrorAsync(
                runId, "internal", "employee-capacity-profile-hours-excel", "row",
                issue.Reason, recordKey: $"row {issue.Row} ({issue.EconomicId})", cancellationToken: cancellationToken);
        }

        await _syncRunService.CompleteRunAsync(
            runId,
            result.Issues.Count == 0 ? "success" : "partial",
            rowsRead: dataRows.Count,
            rowsInserted: result.Created,
            rowsUpdated: result.Updated,
            errorCount: result.Issues.Count,
            cancellationToken: cancellationToken);

        result.Message = "Capacity profile hours import complete.";
        return Ok(result);
    }

    /// <summary>
    /// Bulk-imports legacy flex/overtime opening balances from an uploaded .xlsx sheet
    /// (columns: e-conomic Id, Hours, optional Notes). Every row becomes one
    /// core.overtime_adjustments entry dated to the first of the given effectiveMonth, so
    /// vw_overtime_balance's running total starts from the correct historical figure instead
    /// of zero. Re-running the same file is safe: a row is skipped (and reported) if an
    /// adjustment already exists for that employee and month.
    /// </summary>
    [HttpPost("overtime-adjustments-excel")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<ActionResult<OvertimeAdjustmentImportResult>> ImportOvertimeAdjustmentsExcel(
        IFormFile file,
        [FromQuery] DateOnly effectiveMonth,
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

        var normalizedMonth = new DateOnly(effectiveMonth.Year, effectiveMonth.Month, 1);
        var result = new OvertimeAdjustmentImportResult();
        var now = DateTime.UtcNow;
        var runId = await _syncRunService.StartRunAsync(
            "internal", "overtime-adjustments-excel", ResolveInitiatedBy(),
            notes: $"{file.FileName} (effectiveMonth={normalizedMonth:yyyy-MM})", cancellationToken: cancellationToken);

        await using var stream = file.OpenReadStream();
        using var workbook = new ClosedXML.Excel.XLWorkbook(stream);

        ClosedXML.Excel.IXLWorksheet worksheet;
        ClosedXML.Excel.IXLRow headerRow;
        Dictionary<string, int> headers;
        try
        {
            worksheet = workbook.Worksheets.FirstOrDefault()
                ?? throw new InvalidOperationException("The workbook does not contain any worksheets.");

            headerRow = worksheet.FirstRowUsed()
                ?? throw new InvalidOperationException("The worksheet does not contain a header row.");

            var lastCol = headerRow.LastCellUsed()?.Address.ColumnNumber ?? 0;
            headers = Enumerable.Range(1, lastCol)
                .Select(c => new { Col = c, Header = worksheet.Cell(headerRow.RowNumber(), c).GetString().Trim() })
                .Where(x => !string.IsNullOrWhiteSpace(x.Header))
                .ToDictionary(x => x.Header, x => x.Col, StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            await _syncRunService.LogErrorAsync(
                runId, "internal", "overtime-adjustments-excel", "parse-workbook", ex.Message, cancellationToken: cancellationToken);
            await _syncRunService.CompleteRunAsync(runId, "failed", errorCount: 1, notes: ex.Message, cancellationToken: cancellationToken);
            throw;
        }

        var economicIdCol = FindHeaderColumn(headers, "e-conomic Id", "e-conomic id", "economic id", "economicid");
        var hoursCol = FindHeaderColumn(headers, "Hours", "Timer");
        var notesCol = FindHeaderColumn(headers, "Notes", "Bemærkning", "Bemaerkning");

        if (economicIdCol is null || hoursCol is null)
        {
            const string missingColumnsMessage = "The worksheet must contain columns: e-conomic Id, Hours.";
            await _syncRunService.LogErrorAsync(
                runId, "internal", "overtime-adjustments-excel", "validate-columns", missingColumnsMessage, cancellationToken: cancellationToken);
            await _syncRunService.CompleteRunAsync(runId, "failed", errorCount: 1, notes: missingColumnsMessage, cancellationToken: cancellationToken);
            return BadRequest(new { message = missingColumnsMessage });
        }

        var dataRows = worksheet.RowsUsed().Where(r => r.RowNumber() > headerRow.RowNumber()).ToList();

        foreach (var row in dataRows)
        {
            var rowNumber = row.RowNumber();
            var economicIdText = row.Cell(economicIdCol.Value).GetFormattedString().Trim();

            if (string.IsNullOrWhiteSpace(economicIdText))
            {
                continue; // blank row
            }

            if (!int.TryParse(economicIdText, out var employeeId))
            {
                result.Skipped++;
                result.Issues.Add(new OvertimeAdjustmentImportIssue
                {
                    Row = rowNumber,
                    EconomicId = economicIdText,
                    Reason = "e-conomic Id is not a valid number."
                });
                continue;
            }

            var employeeExists = await _dbContext.Users
                .AsNoTracking()
                .AnyAsync(u => u.EmployeeId == employeeId, cancellationToken);

            if (!employeeExists)
            {
                result.Skipped++;
                result.Issues.Add(new OvertimeAdjustmentImportIssue
                {
                    Row = rowNumber,
                    EconomicId = economicIdText,
                    Reason = "No matching employee found for this e-conomic Id."
                });
                continue;
            }

            var hoursText = row.Cell(hoursCol.Value).GetFormattedString().Trim().Replace(',', '.');

            if (!decimal.TryParse(hoursText, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var hours))
            {
                result.Skipped++;
                result.Issues.Add(new OvertimeAdjustmentImportIssue
                {
                    Row = rowNumber,
                    EconomicId = economicIdText,
                    Reason = $"'{hoursText}' is not a valid hours value."
                });
                continue;
            }

            var alreadyImported = await _dbContext.OvertimeAdjustments
                .AsNoTracking()
                .AnyAsync(a => a.EmployeeId == employeeId && a.EffectiveMonth == normalizedMonth, cancellationToken);

            if (alreadyImported)
            {
                result.Skipped++;
                result.Issues.Add(new OvertimeAdjustmentImportIssue
                {
                    Row = rowNumber,
                    EconomicId = economicIdText,
                    Reason = $"An adjustment already exists for this employee for {normalizedMonth:yyyy-MM}."
                });
                continue;
            }

            var notes = notesCol.HasValue
                ? row.Cell(notesCol.Value).GetFormattedString().Trim()
                : null;

            _dbContext.OvertimeAdjustments.Add(new OvertimeAdjustment
            {
                EmployeeId = employeeId,
                EffectiveMonth = normalizedMonth,
                Hours = hours,
                Notes = string.IsNullOrWhiteSpace(notes) ? $"Legacy opening balance import ({file.FileName})" : notes,
                CreatedAtUtc = now
            });

            result.Created++;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        foreach (var issue in result.Issues)
        {
            await _syncRunService.LogErrorAsync(
                runId, "internal", "overtime-adjustments-excel", "row",
                issue.Reason, recordKey: $"row {issue.Row} ({issue.EconomicId})", cancellationToken: cancellationToken);
        }

        await _syncRunService.CompleteRunAsync(
            runId,
            result.Issues.Count == 0 ? "success" : "partial",
            rowsRead: dataRows.Count,
            rowsInserted: result.Created,
            errorCount: result.Issues.Count,
            cancellationToken: cancellationToken);

        result.Message = "Overtime adjustment import complete.";
        return Ok(result);
    }

    private static int? FindHeaderColumn(IReadOnlyDictionary<string, int> headers, params string[] candidates)
    {
        foreach (var candidate in candidates)
        {
            if (headers.TryGetValue(candidate, out var col))
            {
                return col;
            }
        }

        return null;
    }

    private static decimal GetXlHours(ClosedXML.Excel.IXLRow row, int col)
    {
        var text = row.Cell(col).GetFormattedString().Trim();

        if (string.IsNullOrWhiteSpace(text))
        {
            return 0m;
        }

        var normalized = text.Replace(',', '.');
        return decimal.TryParse(normalized, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var value)
            ? value
            : 0m;
    }

    /// <summary>
    /// Generates monthly capacity periods for all active employees for the given year(s).
    /// Resolves each employee's day-by-day hours from their active capacity profile
    /// (explicit weekday columns, or a shifted default pattern from DefaultWeeklyHours),
    /// then layers any active capacity override on top per day. Public holidays always
    /// zero a day. Already-existing periods are skipped (idempotent).
    /// </summary>
    [HttpPost("employee-capacity")]
    public async Task<ActionResult<CapacityGenerateResult>> GenerateCapacity(
        [FromBody] CapacityGenerateRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Years is null || request.Years.Count == 0)
        {
            return BadRequest(new { message = "At least one year must be provided." });
        }

        var runId = await _syncRunService.StartRunAsync(
            "internal", "employee-capacity", ResolveInitiatedBy(),
            notes: string.Join(",", request.Years), cancellationToken: cancellationToken);

        var employees = await _dbContext.Users
            .AsNoTracking()
            .Where(u => u.IsActive)
            .Select(u => u.EmployeeId)
            .ToListAsync(cancellationToken);

        if (employees.Count == 0)
        {
            await _syncRunService.CompleteRunAsync(runId, "success", rowsRead: 0, notes: "No active employees found.", cancellationToken: cancellationToken);
            return Ok(new CapacityGenerateResult { Message = "No active employees found.", Created = 0, Skipped = 0 });
        }

        // Load all active profiles and overrides for relevant employees up front
        var profiles = await _dbContext.EmployeeCapacityProfiles
            .AsNoTracking()
            .Where(p => p.IsActive && employees.Contains(p.EmployeeId))
            .ToListAsync(cancellationToken);

        var overrides = await _dbContext.EmployeeCapacityOverrides
            .AsNoTracking()
            .Where(o => o.IsActive && employees.Contains(o.EmployeeId))
            .ToListAsync(cancellationToken);

        // Load all relevant DK public holidays up front
        var holidayDates = await _dbContext.PublicHolidayCalendars
            .AsNoTracking()
            .Where(h => h.CountryCode == "DK" && h.IsPublicHoliday && request.Years.Contains(h.HolidayDate.Year))
            .Select(h => h.HolidayDate)
            .ToHashSetAsync(cancellationToken);

        int created = 0;
        int skipped = 0;

        foreach (var year in request.Years)
        {
            for (int month = 1; month <= 12; month++)
            {
                var periodStart = new DateOnly(year, month, 1);
                var periodEnd = new DateOnly(year, month, DateTime.DaysInMonth(year, month));

                foreach (var employeeId in employees)
                {
                    var exists = await _dbContext.EmployeeCapacityPeriods.AnyAsync(
                        p => p.EmployeeId == employeeId &&
                             p.PeriodType == "Month" &&
                             p.PeriodStart == periodStart,
                        cancellationToken);

                    if (exists)
                    {
                        skipped++;
                        continue;
                    }

                    // Find the profile active at the start of this period
                    var profile = CapacityPatternCalculator.ResolveActiveProfile(
                        profiles.Where(p => p.EmployeeId == employeeId), periodStart);

                    var weeklyHours = profile?.DefaultWeeklyHours ?? _capacityDefaults.BaselineWeeklyHours;
                    var profileDailyHours = CapacityPatternCalculator.ResolveProfileDailyHours(profile, weeklyHours, _capacityDefaults);

                    var employeeOverrides = overrides.Where(o => o.EmployeeId == employeeId).ToList();

                    decimal publicHolidayDays = 0;
                    decimal capacityHours = 0;

                    for (var day = periodStart; day <= periodEnd; day = day.AddDays(1))
                    {
                        var activeOverride = CapacityPatternCalculator.ResolveActiveOverride(employeeOverrides, day);

                        var dailyHours = CapacityPatternCalculator.ResolveOverrideDailyHours(activeOverride, weeklyHours, profileDailyHours, _capacityDefaults);
                        var hours = dailyHours[day.DayOfWeek];

                        if (hours <= 0m)
                        {
                            continue;
                        }

                        if (holidayDates.Contains(day))
                        {
                            publicHolidayDays++;
                            continue;
                        }

                        capacityHours += hours;
                    }

                    _dbContext.EmployeeCapacityPeriods.Add(new EmployeeCapacityPeriod
                    {
                        EmployeeId = employeeId,
                        PeriodStart = periodStart,
                        PeriodEnd = periodEnd,
                        PeriodType = "Month",
                        WeeklyHoursBasis = weeklyHours,
                        PublicHolidayDays = publicHolidayDays,
                        CapacityHours = Math.Round(capacityHours, 2),
                        IsGenerated = true,
                        GenerationSource = "capacity-import",
                        GeneratedAtUtc = DateTime.UtcNow
                    });

                    created++;
                }

                await _dbContext.SaveChangesAsync(cancellationToken);
            }
        }

        await _syncRunService.CompleteRunAsync(
            runId, "success", rowsRead: created + skipped, rowsInserted: created, cancellationToken: cancellationToken);

        return Ok(new CapacityGenerateResult
        {
            Message = "Capacity generation complete.",
            Created = created,
            Skipped = skipped
        });
    }

    private sealed class NagerHolidayItem
    {
        [JsonPropertyName("date")]
        public string Date { get; set; } = string.Empty;

        [JsonPropertyName("localName")]
        public string? LocalName { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
    }
}

public sealed class CapacityProfileResult
{
    public string Message { get; set; } = string.Empty;
    public int Created { get; set; }
    public int Skipped { get; set; }
}

public sealed class CapacityProfileHoursImportResult
{
    public string Message { get; set; } = string.Empty;
    public int Created { get; set; }
    public int Updated { get; set; }
    public int Skipped { get; set; }
    public List<CapacityProfileHoursImportIssue> Issues { get; set; } = [];
}

public sealed class CapacityProfileHoursImportIssue
{
    public int Row { get; set; }
    public string EconomicId { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}

public sealed class HolidayImportRequest
{
    public List<int> Years { get; set; } = [];
}

public sealed class HolidayImportResult
{
    public string Message { get; set; } = string.Empty;
    public int Created { get; set; }
    public int Skipped { get; set; }
}

public sealed class CapacityGenerateRequest
{
    public List<int> Years { get; set; } = [];
}

public sealed class CapacityGenerateResult
{
    public string Message { get; set; } = string.Empty;
    public int Created { get; set; }
    public int Skipped { get; set; }
}

public sealed class OvertimeAdjustmentImportResult
{
    public string Message { get; set; } = string.Empty;
    public int Created { get; set; }
    public int Skipped { get; set; }
    public List<OvertimeAdjustmentImportIssue> Issues { get; set; } = [];
}

public sealed class OvertimeAdjustmentImportIssue
{
    public int Row { get; set; }
    public string EconomicId { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}
