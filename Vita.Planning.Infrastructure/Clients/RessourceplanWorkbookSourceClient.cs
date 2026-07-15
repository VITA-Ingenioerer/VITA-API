using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Client;
using Vita.Planning.Application.DTOs;
using Vita.Planning.Application.Interfaces;

namespace Vita.Planning.Infrastructure.Clients;

public sealed class RessourceplanWorkbookSourceClient : IRessourceplanWorkbookSourceClient
{
    private const int MtkodeColumn = 1;
    private const int InitialsColumn = 2;
    private const int ProjectColumn = 3;
    // Column 4 (RestTimer) is intentionally not read: it has no month and isn't imported yet.
    private const int DescriptionColumn = 5;

    private readonly HttpClient _httpClient;
    private readonly MicrosoftGraphSettings _graphSettings;
    private readonly RessourceplanWorkbookSettings _workbookSettings;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public RessourceplanWorkbookSourceClient(
        HttpClient httpClient,
        IOptions<MicrosoftGraphSettings> graphOptions,
        IOptions<RessourceplanWorkbookSettings> workbookOptions)
    {
        _httpClient = httpClient;
        _graphSettings = graphOptions.Value;
        _workbookSettings = workbookOptions.Value;
    }

    public async Task<IReadOnlyList<RessourceplanWorkbookRowDto>> GetRowsAsync(
        CancellationToken cancellationToken = default)
    {
        ValidateSettings();

        var accessToken = await GetAccessTokenAsync(cancellationToken);

        var requestUrl =
            $"https://graph.microsoft.com/v1.0/sites/{_workbookSettings.SiteId}" +
            $"/drives/{_workbookSettings.DriveId}" +
            $"/items/{_workbookSettings.ItemId}" +
            $"/workbook/worksheets/{Uri.EscapeDataString(_workbookSettings.WorksheetName)}/usedRange";

        using var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await _httpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);

            throw new InvalidOperationException(
                $"Could not read Ressourceplane workbook worksheet '{_workbookSettings.WorksheetName}'. " +
                $"StatusCode: {(int)response.StatusCode} {response.ReasonPhrase}. Body: {errorBody}");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);

        var usedRange = await JsonSerializer.DeserializeAsync<UsedRangeResponse>(
            stream,
            JsonOptions,
            cancellationToken);

        if (usedRange?.Values is null || usedRange.Values.Count < 2)
        {
            return [];
        }

        var rows = new List<RessourceplanWorkbookRowDto>();

        // Row 0 is the header row.
        for (var i = 1; i < usedRange.Values.Count; i++)
        {
            var row = usedRange.Values[i];

            var initials = GetCellString(row, InitialsColumn)?.Trim().ToLowerInvariant();

            if (string.IsNullOrWhiteSpace(initials))
            {
                continue;
            }

            var project = GetCellString(row, ProjectColumn)?.Trim();
            var mtkode = GetCellString(row, MtkodeColumn);
            var description = GetCellString(row, DescriptionColumn)?.Trim();

            if (string.IsNullOrWhiteSpace(project) || string.IsNullOrWhiteSpace(mtkode))
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

            // Hours can themselves contain a comma as a Danish decimal separator (e.g.
            // "2026-05,133,2" means 133.2 hours), so only split off the month on the first
            // comma and normalize any remaining comma to a period before parsing.
            var hoursText = entry[(separatorIndex + 1)..].Trim().Replace(',', '.');

            if (!decimal.TryParse(hoursText, NumberStyles.Number, CultureInfo.InvariantCulture, out var hours))
            {
                continue;
            }

            result.Add(new RessourceplanWorkbookMonthHourDto
            {
                MonthYear = monthYear,
                Hours = hours
            });
        }

        return result;
    }

    private static string? GetCellString(IReadOnlyList<JsonElement> row, int index)
    {
        if (index >= row.Count)
        {
            return null;
        }

        var cell = row[index];

        return cell.ValueKind switch
        {
            JsonValueKind.String => cell.GetString(),
            JsonValueKind.Number => FormatNumberCell(cell),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null
        };
    }

    // Excel returns numeric cells (project codes, months) as JSON numbers. Format whole numbers
    // without a decimal point so "213300" doesn't come back as "213300.0" or "2.133E+05".
    private static string FormatNumberCell(JsonElement cell)
    {
        var value = cell.GetDouble();

        return value == Math.Floor(value) && !double.IsInfinity(value)
            ? ((long)value).ToString(CultureInfo.InvariantCulture)
            : value.ToString(CultureInfo.InvariantCulture);
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

    private void ValidateSettings()
    {
        if (string.IsNullOrWhiteSpace(_graphSettings.TenantId))
            throw new InvalidOperationException("MicrosoftGraph:TenantId is missing.");

        if (string.IsNullOrWhiteSpace(_graphSettings.ClientId))
            throw new InvalidOperationException("MicrosoftGraph:ClientId is missing.");

        if (string.IsNullOrWhiteSpace(_graphSettings.ClientSecret))
            throw new InvalidOperationException("MicrosoftGraph:ClientSecret is missing.");

        if (string.IsNullOrWhiteSpace(_workbookSettings.SiteId))
            throw new InvalidOperationException("RessourceplanWorkbook:SiteId is missing.");

        if (string.IsNullOrWhiteSpace(_workbookSettings.DriveId))
            throw new InvalidOperationException("RessourceplanWorkbook:DriveId is missing.");

        if (string.IsNullOrWhiteSpace(_workbookSettings.ItemId))
            throw new InvalidOperationException("RessourceplanWorkbook:ItemId is missing.");

        if (string.IsNullOrWhiteSpace(_workbookSettings.WorksheetName))
            throw new InvalidOperationException("RessourceplanWorkbook:WorksheetName is missing.");
    }

    private sealed class UsedRangeResponse
    {
        public List<List<JsonElement>>? Values { get; set; }
    }
}
