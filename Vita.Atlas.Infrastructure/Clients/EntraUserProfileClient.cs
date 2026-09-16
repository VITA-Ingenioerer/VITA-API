using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Client;
using Vita.Atlas.Application.DTOs;
using Vita.Atlas.Application.Interfaces;

namespace Vita.Atlas.Infrastructure.Clients;

/// <summary>
/// Graph writes for the user fields the medarbejder webpart can edit. Deliberately narrow:
/// department, officeLocation and manager are the only things we set, and everything else on
/// the user is left to Entra and the HR sources feeding it.
///
/// Shares the classification client's credentials on purpose — same app registration, same
/// tenant, and User.ReadWrite.All (which these calls need) is already consented for it.
/// </summary>
public sealed class EntraUserProfileClient : IEntraUserProfileClient
{
    private const string GraphBaseUrl = "https://graph.microsoft.com/v1.0";
    private const string GraphScope = "https://graph.microsoft.com/.default";

    private readonly HttpClient _httpClient;
    private readonly EntraClassificationSettings _settings;

    // Token caching lives on the credential instance, so both are built once and reused.
    private readonly Lazy<IConfidentialClientApplication?> _confidentialClient;
    private readonly Lazy<TokenCredential?> _managedIdentityCredential;

    public EntraUserProfileClient(
        HttpClient httpClient,
        IOptions<EntraClassificationSettings> options)
    {
        _httpClient = httpClient;
        _settings = options.Value;

        _confidentialClient = new Lazy<IConfidentialClientApplication?>(() =>
            string.IsNullOrWhiteSpace(_settings.ClientSecret)
                ? null
                : ConfidentialClientApplicationBuilder
                    .Create(_settings.ClientId)
                    .WithClientSecret(_settings.ClientSecret)
                    .WithAuthority($"https://login.microsoftonline.com/{_settings.TenantId}")
                    .Build());

        _managedIdentityCredential = new Lazy<TokenCredential?>(() =>
            string.IsNullOrWhiteSpace(_settings.ClientSecret)
                ? new DefaultAzureCredential(new DefaultAzureCredentialOptions
                {
                    ManagedIdentityClientId = string.IsNullOrWhiteSpace(_settings.ClientId)
                        ? null
                        : _settings.ClientId
                })
                : null);
    }

    public async Task UpdateProfileAsync(
        string userPrincipalName,
        string? department,
        string? officeLocation,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userPrincipalName))
        {
            throw new InvalidOperationException("A user principal name is required to update an Entra profile.");
        }

        // Null clears the field in Entra; empty string would leave it set to blank, which
        // reads as "configured to nothing" in other tools.
        var payload = new JsonObject
        {
            ["department"] = Normalize(department),
            ["officeLocation"] = Normalize(officeLocation)
        };

        using var response = await SendAsync(
            HttpMethod.Patch,
            $"{GraphBaseUrl}/users/{Uri.EscapeDataString(userPrincipalName)}",
            payload,
            cancellationToken);

        await EnsureSuccessAsync(response, $"update profile for {userPrincipalName}", cancellationToken);
    }

    public async Task UpdateManagerAsync(
        string userPrincipalName,
        string? managerUserPrincipalName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userPrincipalName))
        {
            throw new InvalidOperationException("A user principal name is required to update an Entra manager.");
        }

        var managerUrl = $"{GraphBaseUrl}/users/{Uri.EscapeDataString(userPrincipalName)}/manager/$ref";

        if (string.IsNullOrWhiteSpace(managerUserPrincipalName))
        {
            using var deleteResponse = await SendAsync(HttpMethod.Delete, managerUrl, content: null, cancellationToken);

            // Graph answers 404 when there was no manager to begin with. The caller asked for
            // "no manager" and that is the state either way, so it is not an error.
            if (deleteResponse.StatusCode == HttpStatusCode.NotFound)
            {
                return;
            }

            await EnsureSuccessAsync(deleteResponse, $"clear manager for {userPrincipalName}", cancellationToken);
            return;
        }

        // Graph models the manager as a navigation property, so it is set by reference rather
        // than by PATCHing a field.
        var payload = new JsonObject
        {
            ["@odata.id"] = $"{GraphBaseUrl}/users/{Uri.EscapeDataString(managerUserPrincipalName)}"
        };

        using var response = await SendAsync(HttpMethod.Put, managerUrl, payload, cancellationToken);
        await EnsureSuccessAsync(response, $"set manager for {userPrincipalName}", cancellationToken);
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string url,
        JsonNode? content,
        CancellationToken cancellationToken)
    {
        var accessToken = await GetAccessTokenAsync(cancellationToken);

        using var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        if (content is not null)
        {
            request.Content = JsonContent.Create(content);
        }

        return await _httpClient.SendAsync(request, cancellationToken);
    }

    private static async Task EnsureSuccessAsync(
        HttpResponseMessage response,
        string operation,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        // 403 on these calls is nearly always a consent problem rather than a bad request,
        // and Graph's own message does not say which permission is missing.
        var hint = response.StatusCode == HttpStatusCode.Forbidden
            ? " The app registration likely lacks User.ReadWrite.All (application permission, admin consent granted)."
            : string.Empty;

        throw new InvalidOperationException(
            $"Microsoft Graph request failed ({operation}). " +
            $"StatusCode: {(int)response.StatusCode} {response.ReasonPhrase}.{hint} Body: {body}");
    }

    private async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        if (_confidentialClient.Value is { } app)
        {
            var authResult = await app
                .AcquireTokenForClient([GraphScope])
                .ExecuteAsync(cancellationToken);

            return authResult.AccessToken;
        }

        var credential = _managedIdentityCredential.Value
                         ?? throw new InvalidOperationException(
                             "No Entra credential is configured for user profile writes.");

        var token = await credential.GetTokenAsync(
            new TokenRequestContext([GraphScope]),
            cancellationToken);

        return token.Token;
    }
}
