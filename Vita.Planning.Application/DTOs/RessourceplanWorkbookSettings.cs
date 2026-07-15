namespace Vita.Planning.Application.DTOs;

/// <summary>
/// Locates the "Ressourceplane" Excel workbook in SharePoint that is the actual source of
/// truth for legacy planned hours (replaces the old legacy HTTP API as the import source).
/// </summary>
public sealed class RessourceplanWorkbookSettings
{
    public string SiteId { get; init; } = string.Empty;
    public string DriveId { get; init; } = string.Empty;
    public string ItemId { get; init; } = string.Empty;
    public string WorksheetName { get; init; } = "Timer-tabel";
}
