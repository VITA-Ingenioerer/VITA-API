namespace Vita.Planning.Application.Interfaces;

public interface IOutlookCalendarClient
{
    /// <summary>
    /// Creates an all-day "out of office" event (showAs = oof) in the given user's calendar.
    /// Returns the Microsoft Graph event ID.
    /// </summary>
    Task<string> CreateOutOfOfficeEventAsync(
        string userPrincipalName,
        string title,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken = default);
}
