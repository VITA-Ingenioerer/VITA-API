namespace Vita.Planning.Application.DTOs;

public sealed class ProjectWorkspaceSettings
{
    public string SharePointHostname { get; init; } = string.Empty;
    public string EconomicsWorkbookFolderPath { get; init; } = "C03 Økonomi/C03.10 VITA Projektøkonomi";
    public string EconomicsWorkbookFileName { get; init; } = "VITA_C03_Honorar og opfølgning.xlsm";
    public string OfferArchiveFolderPath { get; init; } = "D1 Grundlag";
    public string DefaultOwnerUserId { get; init; } = string.Empty;

    /// <summary>
    /// Template for the project mailbox email. Supports {yy} (2-digit year) and {year} (4-digit year).
    /// Example: "{yy}-mail@vitaing.dk" → "26-mail@vitaing.dk" for 2026.
    /// </summary>
    public string ProjectMailboxEmailTemplate { get; init; } = string.Empty;

    public string GetMailboxEmail(int year)
    {
        if (string.IsNullOrWhiteSpace(ProjectMailboxEmailTemplate))
            return string.Empty;

        if (year < 2000 || year > 2100)
            throw new InvalidOperationException("Year must be between 2000 and 2100.");

        var yy = (year % 100).ToString("00");

        return ProjectMailboxEmailTemplate
            .Replace("{yy}", yy, StringComparison.OrdinalIgnoreCase)
            .Replace("{year}", year.ToString(), StringComparison.OrdinalIgnoreCase);
    }
}
