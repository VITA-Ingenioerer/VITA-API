using System.Globalization;
using ClosedXML.Excel;
using Vita.Atlas.Application.DTOs;

namespace Vita.Atlas.Api.Controllers;

/// <summary>
/// Reads an uploaded "Ressourceplan - Back end" workbook — both halves of it.
///
/// The same workbook is already read from SharePoint by RessourceplanWorkbookSourceClient, which
/// goes through Graph's usedRange API. This is the upload path for the identical file, so the
/// Timer-tabel column positions and the mtkode grammar below are deliberately the same as that
/// client's; if one changes, both have to.
/// </summary>
public static class RessourceplanWorkbookFile
{
    public const string ProjectsWorksheetName = "Projekter";
    public const string HoursWorksheetName = "Timer-tabel";

    // 1-based here because ClosedXML addresses cells from 1, while the Graph client indexes into
    // a zero-based JSON array. Same columns either way: B mtkode, C initials, D project, F text.
    private const int HoursMtkodeColumn = 2;
    private const int HoursInitialsColumn = 3;
    private const int HoursProjectColumn = 4;
    private const int HoursDescriptionColumn = 6;

    /// <summary>The header cell that locates the Projekter table.</summary>
    private const string ProjectsAnchorHeader = "Projektnr.";

    public sealed class Contents
    {
        public List<PlanningMetadataImportItem> ProjectRows { get; init; } = [];
        public List<RessourceplanWorkbookRowDto> HourRows { get; init; } = [];
        public int ProjectsHeaderRowNumber { get; init; }
        public List<string> Warnings { get; init; } = [];
    }

    public static Contents Parse(Stream stream)
    {
        using var workbook = new XLWorkbook(stream);
        var warnings = new List<string>();

        var projectsSheet = FindWorksheet(workbook, ProjectsWorksheetName);
        var hoursSheet = FindWorksheet(workbook, HoursWorksheetName);

        if (projectsSheet is null)
        {
            warnings.Add($"Worksheet '{ProjectsWorksheetName}' was not found — no project metadata was read.");
        }

        if (hoursSheet is null)
        {
            warnings.Add($"Worksheet '{HoursWorksheetName}' was not found — no hours were read.");
        }

        var (projectRows, headerRowNumber) = projectsSheet is null
            ? ([], 0)
            : ParseProjects(projectsSheet, warnings);

        return new Contents
        {
            ProjectRows = projectRows,
            HourRows = hoursSheet is null ? [] : ParseHours(hoursSheet),
            ProjectsHeaderRowNumber = headerRowNumber,
            Warnings = warnings
        };
    }

    private static IXLWorksheet? FindWorksheet(XLWorkbook workbook, string name) =>
        workbook.Worksheets.FirstOrDefault(w => string.Equals(w.Name, name, StringComparison.OrdinalIgnoreCase));

    private static (List<PlanningMetadataImportItem> Rows, int HeaderRowNumber) ParseProjects(
        IXLWorksheet worksheet,
        List<string> warnings)
    {
        // Not FirstRowUsed(). In the real workbook that is row 2, which holds only the
        // Fa/In/Fr/Sa legend off in columns L-M; the actual table starts at row 9. Anchoring on
        // the header text instead survives rows being added above the table.
        var headerRow = FindHeaderRow(worksheet);

        if (headerRow is null)
        {
            warnings.Add(
                $"No header row containing '{ProjectsAnchorHeader}' was found in '{ProjectsWorksheetName}'.");
            return ([], 0);
        }

        var headerRowNumber = headerRow.RowNumber();
        var lastColumn = headerRow.LastCellUsed()?.Address.ColumnNumber ?? 0;

        var headers = Enumerable.Range(1, lastColumn)
            .Select(column => new { Column = column, Header = worksheet.Cell(headerRowNumber, column).GetString().Trim() })
            .Where(x => !string.IsNullOrWhiteSpace(x.Header))
            .GroupBy(x => x.Header, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Column, StringComparer.OrdinalIgnoreCase);

        var rows = worksheet.RowsUsed()
            .Where(r => r.RowNumber() > headerRowNumber)
            .Select(row => new PlanningMetadataImportItem
            {
                Projektnr = Text(row, headers, "Projektnr.") ?? Text(row, headers, "Projektnr") ?? Text(row, headers, "Tilbudsnr."),
                Projektnavn = Text(row, headers, "Projektnavn"),
                Sandsynlighed = Json(ProbabilityText(row, headers)),
                Kundenr = Json(Text(row, headers, "Kundenr.")),
                Kundenavn = Text(row, headers, "Kundenavn") ?? Text(row, headers, "Kunde"),
                Pl = Text(row, headers, "PL") ?? Text(row, headers, "Ansvarlig person"),
                Projektejer = Text(row, headers, "Projektejer"),
                Honorar = Json(Text(row, headers, "Honorar")),
                Kode = Text(row, headers, "Kode (Fa/In/Fr/Sa)") ?? Text(row, headers, "Kode"),
                IndtastningerPaaProjektet = Json(Text(row, headers, "Er der indtastninger på projektet?")),
                Projektkompleksitet = Text(row, headers, "Projektkompleksitet"),
                SenestAendret = Text(row, headers, "Senest ændret") ?? Text(row, headers, "Sidst redigeret"),
                Konstruktionsklasse = Text(row, headers, "Konstruktionsklasse"),
                Brandklasse = Text(row, headers, "Brandklasse"),
                AnsvarligtKontor = Text(row, headers, "Ansvarligt kontor"),
                Bygherre = Text(row, headers, "Bygherre"),
                Totalentreprenoer = Text(row, headers, "Totalentreprenør"),
                Arkitekt = Text(row, headers, "Arkitekt"),
                OevrigtTeam = Text(row, headers, "Øvrigt team"),
                ForventetStart = Text(row, headers, "Forventet start"),
                ForventetSlut = Text(row, headers, "Forventet slut"),
                Storrelse = Text(row, headers, "Størrelse"),
                Relation = Text(row, headers, "Relation"),
                DatoForAfleveringAfPq = Text(row, headers, "Dato for aflevering af PQ"),
                Status = Text(row, headers, "Status")
            })
            .Where(item => !string.IsNullOrWhiteSpace(item.Projektnr))
            .ToList();

        return (rows, headerRowNumber);
    }

    private static IXLRow? FindHeaderRow(IXLWorksheet worksheet)
    {
        foreach (var row in worksheet.RowsUsed().Take(50))
        {
            var lastColumn = row.LastCellUsed()?.Address.ColumnNumber ?? 0;

            for (var column = 1; column <= lastColumn; column++)
            {
                var value = worksheet.Cell(row.RowNumber(), column).GetString().Trim();

                if (string.Equals(value, ProjectsAnchorHeader, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(value, "Projektnr", StringComparison.OrdinalIgnoreCase))
                {
                    return row;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Sandsynlighed is written as a fraction in this workbook (0.25, 0.5, 1), not as a
    /// percentage, so it is scaled here — at the edge, once — rather than leaving the importer
    /// to guess from the magnitude of each value. A blank cell stays blank and the importer's
    /// own "no percentage stated means certain" default applies.
    /// </summary>
    private static string? ProbabilityText(IXLRow row, IReadOnlyDictionary<string, int> headers)
    {
        var raw = Text(row, headers, "Sandsynlighed");

        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var normalized = raw.Trim().TrimEnd('%').Trim().Replace(',', '.');

        if (!decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out var value))
        {
            // Left as-is so the importer reports it as an unparseable row rather than this
            // silently turning a typo into a number.
            return raw;
        }

        // Only values that can be a fraction are scaled. Anything above 1 is already a
        // percentage — a sheet that has been half-converted should not get 50 turned into 5000.
        var percent = value <= 1m ? value * 100m : value;

        return percent.ToString(CultureInfo.InvariantCulture);
    }

    private static List<RessourceplanWorkbookRowDto> ParseHours(IXLWorksheet worksheet)
    {
        var rows = new List<RessourceplanWorkbookRowDto>();
        var firstRow = worksheet.FirstRowUsed();

        if (firstRow is null)
        {
            return rows;
        }

        foreach (var row in worksheet.RowsUsed().Where(r => r.RowNumber() > firstRow.RowNumber()))
        {
            var initials = worksheet.Cell(row.RowNumber(), HoursInitialsColumn).GetString().Trim().ToLowerInvariant();
            var project = worksheet.Cell(row.RowNumber(), HoursProjectColumn).GetString().Trim();
            var mtkode = worksheet.Cell(row.RowNumber(), HoursMtkodeColumn).GetString();
            var description = worksheet.Cell(row.RowNumber(), HoursDescriptionColumn).GetString().Trim();

            if (string.IsNullOrWhiteSpace(initials) ||
                string.IsNullOrWhiteSpace(project) ||
                string.IsNullOrWhiteSpace(mtkode))
            {
                continue;
            }

            var monthHours = ParseMtkode(mtkode);

            if (monthHours.Count == 0)
            {
                continue;
            }

            rows.Add(new RessourceplanWorkbookRowDto
            {
                Initials = initials,
                Project = project,
                Description = string.IsNullOrWhiteSpace(description) ? null : description,
                MonthHours = monthHours
            });
        }

        return rows;
    }

    /// <summary>
    /// ";2026-07,110;2026-12,30" — semicolon-separated month/hours pairs. Kept byte-for-byte
    /// equivalent to RessourceplanWorkbookSourceClient.ParseMtkode, including that hours may
    /// themselves contain a Danish decimal comma ("2026-05,133,2" is 133.2 hours), so only the
    /// first comma separates the month.
    /// </summary>
    private static List<RessourceplanWorkbookMonthHourDto> ParseMtkode(string mtkode)
    {
        var result = new List<RessourceplanWorkbookMonthHourDto>();
        var entries = mtkode.Trim().Trim(';').Split(';', StringSplitOptions.RemoveEmptyEntries);

        foreach (var entry in entries)
        {
            var separatorIndex = entry.IndexOf(',');

            if (separatorIndex < 0)
            {
                continue;
            }

            var monthYear = entry[..separatorIndex].Trim();
            var hoursText = entry[(separatorIndex + 1)..].Trim().Replace(',', '.');

            if (!decimal.TryParse(hoursText, NumberStyles.Number, CultureInfo.InvariantCulture, out var hours))
            {
                continue;
            }

            result.Add(new RessourceplanWorkbookMonthHourDto { MonthYear = monthYear, Hours = hours });
        }

        return result;
    }

    private static string? Text(IXLRow row, IReadOnlyDictionary<string, int> headers, string header)
    {
        if (!headers.TryGetValue(header, out var column))
        {
            return null;
        }

        var cell = row.Cell(column);

        if (cell.IsEmpty())
        {
            return null;
        }

        // Dates have to be formatted rather than ToString()'d, or a real date cell arrives as an
        // OADate serial number and every downstream parse of it fails.
        var value = cell.DataType == XLDataType.DateTime && cell.TryGetValue<DateTime>(out var date)
            ? date.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
            : cell.GetString();

        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static System.Text.Json.JsonElement? Json(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        // The import items model these as JsonElement because the JSON twin of this importer
        // receives them as raw JSON. A scalar string round-trips through both number and text
        // parsing downstream, so it is the safe representation for a spreadsheet cell.
        using var document = System.Text.Json.JsonDocument.Parse(
            System.Text.Json.JsonSerializer.Serialize(value));

        return document.RootElement.Clone();
    }
}
