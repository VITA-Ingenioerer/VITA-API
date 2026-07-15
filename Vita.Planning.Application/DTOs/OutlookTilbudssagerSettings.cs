namespace Vita.Planning.Application.DTOs;

public sealed class OutlookTilbudssagerSettings
{
    public string MailboxDisplayNameTemplate { get; init; } = "{yy} - Tilbudssager - VITA";

    public string MailboxEmailTemplate { get; init; } = "{yy}-tilbud@vitaing.dk";

    public string GetMailboxDisplayName(int year)
    {
        var yy = GetShortYear(year);

        return MailboxDisplayNameTemplate
            .Replace("{yy}", yy, StringComparison.OrdinalIgnoreCase)
            .Replace("{year}", year.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    public string GetMailboxEmail(int year)
    {
        var yy = GetShortYear(year);

        return MailboxEmailTemplate
            .Replace("{yy}", yy, StringComparison.OrdinalIgnoreCase)
            .Replace("{year}", year.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    private static string GetShortYear(int year)
    {
        if (year < 2000 || year > 2100)
        {
            throw new InvalidOperationException("Year must be between 2000 and 2100.");
        }

        return (year % 100).ToString("00");
    }
}