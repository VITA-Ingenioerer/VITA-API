using Microsoft.Extensions.Options;
using Microsoft.Identity.Client;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Vita.Planning.Application.DTOs;
using Vita.Planning.Application.Interfaces;

namespace Vita.Planning.Infrastructure.Clients;

public sealed class SharePointOfferArchiveClient : ISharePointOfferArchiveClient
{

    private static readonly Regex InvalidFolderNameCharacters = new(
    @"[""*:<>?/\\|]",
    RegexOptions.Compiled);
    private readonly HttpClient _httpClient;
    private readonly MicrosoftGraphSettings _graphSettings;
    private readonly TilbudssagerSettings _tilbudssagerSettings;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public SharePointOfferArchiveClient(
        HttpClient httpClient,
        IOptions<MicrosoftGraphSettings> graphOptions,
        IOptions<TilbudssagerSettings> tilbudssagerOptions)
    {
        _httpClient = httpClient;
        _graphSettings = graphOptions.Value;
        _tilbudssagerSettings = tilbudssagerOptions.Value;
    }
    public async Task<CreateTilbudssagerFolderResultDto> CreateOfferFolderAsync(
    CreateTilbudssagerFolderRequest request,
    string createdByUserName,
    CancellationToken cancellationToken = default)
    {
        ValidateSettings();

        if (request.Year < 2000 || request.Year > 2100)
        {
            throw new InvalidOperationException("Year must be between 2000 and 2100.");
        }

        if (string.IsNullOrWhiteSpace(request.OfferNumber))
        {
            throw new InvalidOperationException("Offer number is required.");
        }

        if (string.IsNullOrWhiteSpace(request.ProjectName))
        {
            throw new InvalidOperationException("Project name is required.");
        }

        var accessToken = await GetAccessTokenAsync(cancellationToken);

        var yearFolder = await ResolveYearFolderAsync(
            request.Year,
            cancellationToken);

        var folderName = BuildOfferFolderName(
            request.OfferNumber,
            request.ProjectName,
            createdByUserName);

        var createUrl =
            $"https://graph.microsoft.com/v1.0/drives/{Uri.EscapeDataString(_tilbudssagerSettings.DocumentLibraryDriveId)}/items/{Uri.EscapeDataString(yearFolder.YearFolderItemId)}/children";

        var json = $$"""
    {
      "name": "{{JsonEscape(folderName)}}",
      "folder": {},
      "@microsoft.graph.conflictBehavior": "fail"
    }
    """;

        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, createUrl);
        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        requestMessage.Content = new StringContent(json, Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(requestMessage, cancellationToken);

        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            throw new InvalidOperationException(
                $"A folder named '{folderName}' already exists in '{yearFolder.YearFolderName}'.");
        }

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);

            throw new InvalidOperationException(
                $"Could not create offer folder '{folderName}'. StatusCode: {(int)response.StatusCode} {response.ReasonPhrase}. Body: {errorBody}");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);

        var createdItem = await JsonSerializer.DeserializeAsync<GraphDriveItemResponse>(
            stream,
            JsonOptions,
            cancellationToken);

        if (createdItem is null || string.IsNullOrWhiteSpace(createdItem.Id))
        {
            throw new InvalidOperationException(
                $"Microsoft Graph returned an empty response after creating folder '{folderName}'.");
        }

        var templateWorkbook = await ResolveTemplateWorkbookAsync(
            accessToken,
            cancellationToken);

        var copyMonitorUrl = await CopyTemplateWorkbookToOfferFolderAsync(
            accessToken,
            templateWorkbook.Id!,
            createdItem.Id,
            cancellationToken);

        return new CreateTilbudssagerFolderResultDto
        {
            SiteId = yearFolder.SiteId,
            SiteWebUrl = yearFolder.SiteWebUrl,
            DocumentLibraryDriveId = yearFolder.DocumentLibraryDriveId,
            Year = request.Year,
            YearFolderName = yearFolder.YearFolderName,
            YearFolderItemId = yearFolder.YearFolderItemId,
            YearFolderWebUrl = yearFolder.YearFolderWebUrl,
            OfferFolderName = createdItem.Name ?? folderName,
            OfferFolderItemId = createdItem.Id,
            OfferFolderWebUrl = createdItem.WebUrl ?? string.Empty,
            OfferFolderPath = $"{yearFolder.YearFolderName}/{createdItem.Name ?? folderName}",
            CreatedByUserName = createdByUserName,
            CreatedAtUtc = DateTime.UtcNow,

            TemplateWorkbookSourceDriveId = _tilbudssagerSettings.TemplateDocumentLibraryDriveId,
            TemplateWorkbookSourcePath = _tilbudssagerSettings.TemplateWorkbookPath,
            TemplateWorkbookSourceItemId = templateWorkbook.Id,
            TemplateWorkbookSourceName = templateWorkbook.Name,
            TemplateWorkbookSourceWebUrl = templateWorkbook.WebUrl,
            TemplateWorkbookCopyStatus = "accepted",
            TemplateWorkbookCopyMonitorUrl = copyMonitorUrl
        };
    }
    public async Task<CreateTilbudssagerFolderResultDto> ResolveYearFolderAsync(
        int year,
        CancellationToken cancellationToken = default)
    {
        ValidateSettings();

        if (year < 2000 || year > 2100)
        {
            throw new InvalidOperationException("Year must be between 2000 and 2100.");
        }

        var accessToken = await GetAccessTokenAsync(cancellationToken);

        var yearFolderName = _tilbudssagerSettings.GetYearFolderName(year);

        var escapedFolderPath = Uri.EscapeDataString(yearFolderName);
        var requestUrl =
            $"https://graph.microsoft.com/v1.0/drives/{Uri.EscapeDataString(_tilbudssagerSettings.DocumentLibraryDriveId)}/root:/{escapedFolderPath}:";


        using var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await _httpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);

            throw new InvalidOperationException(
                $"Could not resolve Tilbudssager year folder '{yearFolderName}'. StatusCode: {(int)response.StatusCode} {response.ReasonPhrase}. Body: {errorBody}");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);

        var graphItem = await JsonSerializer.DeserializeAsync<GraphDriveItemResponse>(
            stream,
            JsonOptions,
            cancellationToken);

        if (graphItem is null || string.IsNullOrWhiteSpace(graphItem.Id))
        {
            throw new InvalidOperationException(
                $"Microsoft Graph returned an empty response for Tilbudssager year folder '{yearFolderName}'.");
        }

        return new CreateTilbudssagerFolderResultDto
        {
            SiteId = _tilbudssagerSettings.SiteId,
            SiteWebUrl = _tilbudssagerSettings.WebUrl,
            DocumentLibraryDriveId = _tilbudssagerSettings.DocumentLibraryDriveId,
            Year = year,
            YearFolderName = yearFolderName,
            YearFolderItemId = graphItem.Id,
            YearFolderWebUrl = graphItem.WebUrl ?? string.Empty
        };
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
    private static string EscapeGraphPath(string path)
    {
        return string.Join(
            "/",
            path
                .Split('/', StringSplitOptions.RemoveEmptyEntries)
                .Select(Uri.EscapeDataString));
    }
    private async Task<string?> CopyTemplateWorkbookToOfferFolderAsync(
    string accessToken,
    string templateWorkbookItemId,
    string offerFolderItemId,
    CancellationToken cancellationToken)
    {
        var requestUrl =
            $"https://graph.microsoft.com/v1.0/drives/{Uri.EscapeDataString(_tilbudssagerSettings.TemplateDocumentLibraryDriveId)}/items/{Uri.EscapeDataString(templateWorkbookItemId)}/copy";

        var json = $$"""
    {
      "parentReference": {
        "driveId": "{{JsonEscape(_tilbudssagerSettings.DocumentLibraryDriveId)}}",
        "id": "{{JsonEscape(offerFolderItemId)}}"
      }
    }
    """;

        using var request = new HttpRequestMessage(HttpMethod.Post, requestUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(request, cancellationToken);

        if (response.StatusCode != HttpStatusCode.Accepted)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);

            throw new InvalidOperationException(
                $"Could not copy template workbook into offer folder. StatusCode: {(int)response.StatusCode} {response.ReasonPhrase}. Body: {errorBody}");
        }

        return response.Headers.Location?.ToString();
    }
    private async Task<GraphDriveItemResponse> ResolveTemplateWorkbookAsync(
    string accessToken,
    CancellationToken cancellationToken)
    {
        var escapedPath = EscapeGraphPath(_tilbudssagerSettings.TemplateWorkbookPath);

        var requestUrl =
            $"https://graph.microsoft.com/v1.0/drives/{Uri.EscapeDataString(_tilbudssagerSettings.TemplateDocumentLibraryDriveId)}/root:/{escapedPath}:";

        using var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await _httpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);

            throw new InvalidOperationException(
                $"Could not resolve template workbook '{_tilbudssagerSettings.TemplateWorkbookPath}'. StatusCode: {(int)response.StatusCode} {response.ReasonPhrase}. Body: {errorBody}");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);

        var item = await JsonSerializer.DeserializeAsync<GraphDriveItemResponse>(
            stream,
            JsonOptions,
            cancellationToken);

        if (item is null || string.IsNullOrWhiteSpace(item.Id))
        {
            throw new InvalidOperationException(
                $"Microsoft Graph returned an empty response for template workbook '{_tilbudssagerSettings.TemplateWorkbookPath}'.");
        }

        return item;
    }
    private void ValidateSettings()
    {
        if (string.IsNullOrWhiteSpace(_graphSettings.TenantId))
        {
            throw new InvalidOperationException("MicrosoftGraph:TenantId is missing.");
        }

        if (string.IsNullOrWhiteSpace(_graphSettings.ClientId))
        {
            throw new InvalidOperationException("MicrosoftGraph:ClientId is missing.");
        }

        if (string.IsNullOrWhiteSpace(_graphSettings.ClientSecret))
        {
            throw new InvalidOperationException("MicrosoftGraph:ClientSecret is missing.");
        }

        if (string.IsNullOrWhiteSpace(_tilbudssagerSettings.SiteId))
        {
            throw new InvalidOperationException("Tilbudssager:SiteId is missing.");
        }

        if (string.IsNullOrWhiteSpace(_tilbudssagerSettings.WebUrl))
        {
            throw new InvalidOperationException("Tilbudssager:WebUrl is missing.");
        }

        if (string.IsNullOrWhiteSpace(_tilbudssagerSettings.DocumentLibraryDriveId))
        {
            throw new InvalidOperationException("Tilbudssager:DocumentLibraryDriveId is missing.");
        }
        if (string.IsNullOrWhiteSpace(_tilbudssagerSettings.TemplateDocumentLibraryDriveId))
        {
            throw new InvalidOperationException("Tilbudssager:TemplateDocumentLibraryDriveId is missing.");
        }

        if (string.IsNullOrWhiteSpace(_tilbudssagerSettings.TemplateWorkbookPath))
        {
            throw new InvalidOperationException("Tilbudssager:TemplateWorkbookPath is missing.");
        }
    }

    private sealed class GraphDriveItemResponse
    {
        public string? Id { get; init; }

        public string? Name { get; init; }

        public string? WebUrl { get; init; }

        public GraphFolderResponse? Folder { get; init; }
    }
    private static string BuildOfferFolderName(
    string offerNumber,
    string projectName,
    string createdByUserName)
    {
        var cleanOfferNumber = SanitizeFolderNamePart(offerNumber).ToUpperInvariant();
        var cleanProjectName = SanitizeFolderNamePart(projectName);
        var cleanUserName = SanitizeFolderNamePart(createdByUserName).ToUpperInvariant();

        var folderName = $"{cleanOfferNumber} - {cleanProjectName} - {cleanUserName}";

        if (folderName.Length <= 180)
        {
            return folderName;
        }

        var maxProjectNameLength = Math.Max(20, 180 - cleanOfferNumber.Length - cleanUserName.Length - 6);
        cleanProjectName = cleanProjectName.Length <= maxProjectNameLength
            ? cleanProjectName
            : cleanProjectName[..maxProjectNameLength].Trim();

        return $"{cleanOfferNumber} - {cleanProjectName} - {cleanUserName}";
    }

    private static string SanitizeFolderNamePart(string value)
    {
        var cleaned = InvalidFolderNameCharacters
            .Replace(value.Trim(), "-");

        cleaned = Regex.Replace(cleaned, @"\s+", " ");

        cleaned = cleaned.Trim('.', ' ');

        return string.IsNullOrWhiteSpace(cleaned)
            ? "Unknown"
            : cleaned;
    }

    private static string JsonEscape(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
    }
    private sealed class GraphFolderResponse
    {
        public int? ChildCount { get; init; }
    }
}