using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Client;
using Vita.Planning.Application.DTOs;
using Vita.Planning.Application.Interfaces;

namespace Vita.Planning.Infrastructure.Clients;

public sealed class OutlookTilbudssagerClient : IOutlookTilbudssagerClient
{
    private readonly HttpClient _httpClient;
    private readonly MicrosoftGraphSettings _graphSettings;
    private readonly OutlookTilbudssagerSettings _settings;

    private static readonly Regex InvalidFolderNameCharacters = new(
        @"[""*:<>?/\\|]",
        RegexOptions.Compiled);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public OutlookTilbudssagerClient(
        HttpClient httpClient,
        IOptions<MicrosoftGraphSettings> graphOptions,
        IOptions<OutlookTilbudssagerSettings> options)
    {
        _httpClient = httpClient;
        _graphSettings = graphOptions.Value;
        _settings = options.Value;
    }

    public async Task<CreateOutlookTilbudssagerFolderResultDto> CreateOfferFolderAsync(
        CreateOutlookTilbudssagerFolderRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateGraphSettings();

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

        var mailboxEmail = _settings.GetMailboxEmail(request.Year);
        var mailboxDisplayName = _settings.GetMailboxDisplayName(request.Year);
        var folderName = BuildFolderName(request.OfferNumber, request.ProjectName);

        var accessToken = await GetAccessTokenAsync(cancellationToken);

        var requestUrl =
            $"https://graph.microsoft.com/v1.0/users/{Uri.EscapeDataString(mailboxEmail)}/mailFolders";

        var json = $$"""
        {
          "displayName": "{{JsonEscape(folderName)}}",
          "isHidden": false
        }
        """;

        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, requestUrl);
        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        requestMessage.Content = new StringContent(json, Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(requestMessage, cancellationToken);

        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            throw new InvalidOperationException(
                $"A mail folder named '{folderName}' already exists in mailbox '{mailboxEmail}'.");
        }

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);

            throw new InvalidOperationException(
                $"Could not create Outlook offer folder '{folderName}' in mailbox '{mailboxEmail}'. StatusCode: {(int)response.StatusCode} {response.ReasonPhrase}. Body: {errorBody}");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);

        var createdFolder = await JsonSerializer.DeserializeAsync<GraphMailFolderResponse>(
            stream,
            JsonOptions,
            cancellationToken);

        if (createdFolder is null || string.IsNullOrWhiteSpace(createdFolder.Id))
        {
            throw new InvalidOperationException(
                $"Microsoft Graph returned an empty response after creating Outlook folder '{folderName}'.");
        }

        return new CreateOutlookTilbudssagerFolderResultDto
        {
            Year = request.Year,
            MailboxDisplayName = mailboxDisplayName,
            MailboxEmail = mailboxEmail,
            FolderDisplayName = createdFolder.DisplayName ?? folderName,
            FolderId = createdFolder.Id,
            ParentFolderId = createdFolder.ParentFolderId,
            ChildFolderCount = createdFolder.ChildFolderCount,
            UnreadItemCount = createdFolder.UnreadItemCount,
            TotalItemCount = createdFolder.TotalItemCount,
            FolderLocation = $"{mailboxEmail} / {createdFolder.DisplayName ?? folderName}",
            CreatedAtUtc = DateTime.UtcNow
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

    private void ValidateGraphSettings()
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
    }

    private static string BuildFolderName(string offerNumber, string projectName)
    {
        var cleanOfferNumber = SanitizeFolderNamePart(offerNumber).ToUpperInvariant();
        var cleanProjectName = SanitizeFolderNamePart(projectName);

        var folderName = $"{cleanOfferNumber} - {cleanProjectName}";

        if (folderName.Length <= 180)
        {
            return folderName;
        }

        var maxProjectNameLength = Math.Max(20, 180 - cleanOfferNumber.Length - 3);

        cleanProjectName = cleanProjectName.Length <= maxProjectNameLength
            ? cleanProjectName
            : cleanProjectName[..maxProjectNameLength].Trim();

        return $"{cleanOfferNumber} - {cleanProjectName}";
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

    private sealed class GraphMailFolderResponse
    {
        public string? Id { get; init; }

        public string? DisplayName { get; init; }

        public string? ParentFolderId { get; init; }

        public int? ChildFolderCount { get; init; }

        public int? UnreadItemCount { get; init; }

        public int? TotalItemCount { get; init; }

        public bool? IsHidden { get; init; }
    }
}