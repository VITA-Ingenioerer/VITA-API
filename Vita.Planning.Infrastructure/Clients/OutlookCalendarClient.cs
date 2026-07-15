using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Client;
using Vita.Planning.Application.DTOs;
using Vita.Planning.Application.Interfaces;

namespace Vita.Planning.Infrastructure.Clients;

public sealed class OutlookCalendarClient : IOutlookCalendarClient
{
    // Windows time zone ID for Denmark (CET/CEST) — required by Graph's DateTimeTimeZone.
    private const string TimeZoneId = "Romance Standard Time";

    private readonly HttpClient _httpClient;
    private readonly MicrosoftGraphSettings _graphSettings;
    private readonly ILogger<OutlookCalendarClient> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public OutlookCalendarClient(
        HttpClient httpClient,
        IOptions<MicrosoftGraphSettings> graphOptions,
        ILogger<OutlookCalendarClient> logger)
    {
        _httpClient = httpClient;
        _graphSettings = graphOptions.Value;
        _logger = logger;
    }

    public async Task<string> CreateOutOfOfficeEventAsync(
        string userPrincipalName,
        string title,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken = default)
    {
        ValidateGraphSettings();

        var accessToken = await GetAccessTokenAsync(cancellationToken);

        // Graph all-day events use an exclusive end date at midnight, so a range meant to
        // cover startDate..endDate inclusive needs endDate + 1 day here.
        var payload = new
        {
            subject = title,
            isAllDay = true,
            showAs = "oof",
            start = new { dateTime = $"{startDate:yyyy-MM-dd}T00:00:00", timeZone = TimeZoneId },
            end = new { dateTime = $"{endDate.AddDays(1):yyyy-MM-dd}T00:00:00", timeZone = TimeZoneId }
        };

        using var requestMessage = new HttpRequestMessage(
            HttpMethod.Post,
            $"https://graph.microsoft.com/v1.0/users/{Uri.EscapeDataString(userPrincipalName)}/events");
        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        requestMessage.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(requestMessage, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException(
                $"Failed to create out-of-office calendar event for '{userPrincipalName}'. " +
                $"StatusCode: {(int)response.StatusCode}. Body: {body}");
        }

        // The event has already been created in the calendar at this point (Graph
        // returned success) — a problem reading/parsing the response body from here
        // on must not be reported as an overall failure, since the actual requested
        // operation already succeeded. Losing the event ID only degrades the audit
        // trail, not the calendar entry itself.
        try
        {
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var created = await JsonSerializer.DeserializeAsync<GraphIdResponse>(stream, JsonOptions, cancellationToken);

            if (!string.IsNullOrWhiteSpace(created?.Id))
            {
                return created.Id;
            }

            _logger.LogWarning(
                "Microsoft Graph returned a successful status for the out-of-office event for '{UserPrincipalName}' but no event ID could be read from the response body.",
                userPrincipalName);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "The out-of-office calendar event for '{UserPrincipalName}' was created successfully, but the response body could not be parsed to extract its ID.",
                userPrincipalName);
        }

        return string.Empty;
    }

    private async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        var app = ConfidentialClientApplicationBuilder
            .Create(_graphSettings.ClientId)
            .WithClientSecret(_graphSettings.ClientSecret)
            .WithAuthority($"https://login.microsoftonline.com/{_graphSettings.TenantId}")
            .Build();

        var authResult = await app
            .AcquireTokenForClient(["https://graph.microsoft.com/.default"])
            .ExecuteAsync(cancellationToken);

        return authResult.AccessToken;
    }

    private void ValidateGraphSettings()
    {
        if (string.IsNullOrWhiteSpace(_graphSettings.TenantId))
            throw new InvalidOperationException("MicrosoftGraph:TenantId is not configured.");

        if (string.IsNullOrWhiteSpace(_graphSettings.ClientId))
            throw new InvalidOperationException("MicrosoftGraph:ClientId is not configured.");

        if (string.IsNullOrWhiteSpace(_graphSettings.ClientSecret))
            throw new InvalidOperationException("MicrosoftGraph:ClientSecret is not configured.");
    }

    private sealed class GraphIdResponse
    {
        public string? Id { get; init; }
    }
}
