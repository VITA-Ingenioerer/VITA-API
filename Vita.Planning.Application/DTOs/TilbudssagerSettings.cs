using System;
using System.Collections.Generic;
using System.Text;

namespace Vita.Planning.Application.DTOs;

public sealed class TilbudssagerSettings
{
    public string SiteId { get; init; } = string.Empty;

    public string WebUrl { get; init; } = string.Empty;

    public string DocumentLibraryDriveId { get; init; } = string.Empty;
    public string TemplateDocumentLibraryDriveId { get; init; } = string.Empty;

    public string TemplateWorkbookPath { get; init; } =
        "C03 Økonomi/VITA_C03_Honorar og opfølgning.xlsm";

    public string YearFolderNameTemplate { get; init; } = "Tilbud {year}";

    public Dictionary<string, string> YearFolders { get; init; } = [];

    public string GetYearFolderName(int year)
    {
        var key = year.ToString();

        if (YearFolders.TryGetValue(key, out var configuredFolderName)
            && !string.IsNullOrWhiteSpace(configuredFolderName))
        {
            return configuredFolderName.Trim();
        }

        return YearFolderNameTemplate.Replace("{year}", key);
    }
}