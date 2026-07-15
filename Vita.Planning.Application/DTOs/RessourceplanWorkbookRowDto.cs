namespace Vita.Planning.Application.DTOs;

/// <summary>
/// One row from the Ressourceplane workbook's "Timer-tabel" sheet, parsed but not yet resolved
/// against ext.users (Initials is the raw email-prefix from the sheet, e.g. "thom").
/// RestTimer is intentionally not carried here — it has no month and isn't imported (yet).
/// </summary>
public sealed class RessourceplanWorkbookRowDto
{
    public string Initials { get; set; } = string.Empty;
    public string Project { get; set; } = string.Empty;
    public string? Description { get; set; }
    public IReadOnlyList<RessourceplanWorkbookMonthHourDto> MonthHours { get; set; } = [];
}

public sealed class RessourceplanWorkbookMonthHourDto
{
    /// <summary>"yyyy-MM", e.g. "2026-07".</summary>
    public string MonthYear { get; set; } = string.Empty;
    public decimal Hours { get; set; }
}
