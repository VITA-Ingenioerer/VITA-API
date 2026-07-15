namespace Vita.Planning.Application.DTOs;

public sealed class CreateTilbudssagerFolderResultDto
{
    public string SiteId { get; init; } = string.Empty;

    public string SiteWebUrl { get; init; } = string.Empty;

    public string DocumentLibraryDriveId { get; init; } = string.Empty;

    public int Year { get; init; }

    public string YearFolderName { get; init; } = string.Empty;

    public string YearFolderItemId { get; init; } = string.Empty;

    public string YearFolderWebUrl { get; init; } = string.Empty;

    public string OfferFolderName { get; init; } = string.Empty;

    public string OfferFolderItemId { get; init; } = string.Empty;

    public string OfferFolderWebUrl { get; init; } = string.Empty;

    public string OfferFolderPath { get; init; } = string.Empty;

    public string CreatedByUserName { get; init; } = string.Empty;

    public DateTime CreatedAtUtc { get; init; }

    public string? TemplateWorkbookSourceDriveId { get; init; }

    public string? TemplateWorkbookSourcePath { get; init; }

    public string? TemplateWorkbookSourceItemId { get; init; }

    public string? TemplateWorkbookSourceName { get; init; }

    public string? TemplateWorkbookSourceWebUrl { get; init; }

    public string? TemplateWorkbookCopyStatus { get; init; }

    public string? TemplateWorkbookCopyMonitorUrl { get; init; }
}