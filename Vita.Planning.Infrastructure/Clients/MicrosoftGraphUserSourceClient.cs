using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Client;
using Vita.Planning.Application.DTOs;
using Vita.Planning.Application.Interfaces;

namespace Vita.Planning.Infrastructure.Clients;

public sealed class MicrosoftGraphUserSourceClient : IMicrosoftGraphUserSourceClient
{
    private readonly HttpClient _httpClient;
    private readonly MicrosoftGraphSettings _settings;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public MicrosoftGraphUserSourceClient(
        HttpClient httpClient,
        IOptions<MicrosoftGraphSettings> options)
    {
        _httpClient = httpClient;
        _settings = options.Value;
    }

    public async Task<IReadOnlyList<MicrosoftGraphUserDto>> GetUsersAsync(
        CancellationToken cancellationToken = default)
    {
        ValidateSettings();

        var accessToken = await GetAccessTokenAsync(cancellationToken);

        var users = await GetGraphUsersAsync(accessToken, cancellationToken);

        await AddManagersAsync(users, accessToken, cancellationToken);

        return users;
    }

    private async Task<List<MicrosoftGraphUserDto>> GetGraphUsersAsync(
        string accessToken,
        CancellationToken cancellationToken)
    {
        var result = new List<MicrosoftGraphUserDto>();

        var requestUrl =
            "https://graph.microsoft.com/v1.0/users" +
            "?$select=id,employeeId,userPrincipalName,mail,displayName,officeLocation,department,employeeType,jobTitle,mobilePhone,accountEnabled" +
            "&$top=999";

        while (!string.IsNullOrWhiteSpace(requestUrl))
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            using var response = await _httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);

                throw new InvalidOperationException(
                    $"Microsoft Graph users request failed. StatusCode: {(int)response.StatusCode} {response.ReasonPhrase}. Body: {errorBody}");
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);

            var payload = await JsonSerializer.DeserializeAsync<GraphUserListResponse>(
                stream,
                JsonOptions,
                cancellationToken);

            if (payload?.Value is not null)
            {
                foreach (var graphUser in payload.Value)
                {
                    if (string.IsNullOrWhiteSpace(graphUser.Id))
                    {
                        continue;
                    }

                    result.Add(new MicrosoftGraphUserDto
                    {
                        Id = graphUser.Id,
                        EmployeeId = graphUser.EmployeeId,
                        UserPrincipalName = graphUser.UserPrincipalName,
                        Mail = graphUser.Mail,
                        DisplayName = graphUser.DisplayName ?? string.Empty,
                        OfficeLocation = graphUser.OfficeLocation,
                        Department = graphUser.Department,
                        EmployeeType = graphUser.EmployeeType,
                        JobTitle = graphUser.JobTitle,
                        MobilePhone = graphUser.MobilePhone,
                        AccountEnabled = graphUser.AccountEnabled
                    });
                }
            }

            requestUrl = payload?.NextLink;
        }

        return result;
    }

    private async Task AddManagersAsync(
        List<MicrosoftGraphUserDto> users,
        string accessToken,
        CancellationToken cancellationToken)
    {
        foreach (var user in users)
        {
            if (string.IsNullOrWhiteSpace(user.Id))
            {
                continue;
            }

            var requestUrl =
                $"https://graph.microsoft.com/v1.0/users/{Uri.EscapeDataString(user.Id)}/manager" +
                "?$select=id,userPrincipalName,displayName";

            using var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            using var response = await _httpClient.SendAsync(request, cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                continue;
            }

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);

                throw new InvalidOperationException(
                    $"Microsoft Graph manager request failed for user '{user.UserPrincipalName ?? user.Id}'. StatusCode: {(int)response.StatusCode} {response.ReasonPhrase}. Body: {errorBody}");
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);

            var manager = await JsonSerializer.DeserializeAsync<GraphManagerResponse>(
                stream,
                JsonOptions,
                cancellationToken);

            user.ManagerId = manager?.Id;
            user.ManagerUserPrincipalName = manager?.UserPrincipalName;
            user.ManagerDisplayName = manager?.DisplayName;
        }
    }

    private async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        var app = ConfidentialClientApplicationBuilder
            .Create(_settings.ClientId)
            .WithClientSecret(_settings.ClientSecret)
            .WithAuthority($"https://login.microsoftonline.com/{_settings.TenantId}")
            .Build();

        var authResult = await app
            .AcquireTokenForClient(["https://graph.microsoft.com/.default"])
            .ExecuteAsync(cancellationToken);

        return authResult.AccessToken;
    }

    private void ValidateSettings()
    {
        if (string.IsNullOrWhiteSpace(_settings.TenantId))
        {
            throw new InvalidOperationException("MicrosoftGraph:TenantId is missing.");
        }

        if (string.IsNullOrWhiteSpace(_settings.ClientId))
        {
            throw new InvalidOperationException("MicrosoftGraph:ClientId is missing.");
        }

        if (string.IsNullOrWhiteSpace(_settings.ClientSecret))
        {
            throw new InvalidOperationException("MicrosoftGraph:ClientSecret is missing.");
        }
    }

    private sealed class GraphUserListResponse
    {
        public List<GraphUserResponse> Value { get; init; } = [];

        [JsonPropertyName("@odata.nextLink")]
        public string? NextLink { get; init; }
    }

    private sealed class GraphUserResponse
    {
        public string? Id { get; init; }

        public string? EmployeeId { get; init; }

        public string? UserPrincipalName { get; init; }

        public string? Mail { get; init; }

        public string? DisplayName { get; init; }

        public string? OfficeLocation { get; init; }

        public string? Department { get; init; }

        public string? EmployeeType { get; init; }

        public string? JobTitle { get; init; }

        public string? MobilePhone { get; init; }

        public bool? AccountEnabled { get; init; }
    }

    private sealed class GraphManagerResponse
    {
        public string? Id { get; init; }

        public string? UserPrincipalName { get; init; }

        public string? DisplayName { get; init; }
    }
}