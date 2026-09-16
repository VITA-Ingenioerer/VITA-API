using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Client;
using Vita.Atlas.Application.DTOs;
using Vita.Atlas.Application.Interfaces;

namespace Vita.Atlas.Infrastructure.Clients;

/// <summary>
/// Microsoft Graph access to the VITA employee-classification custom security attributes
/// and their extensionAttribute mirrors. Knows the Graph wire format only — validation,
/// auditing and the SQL read model belong to EmployeeIdentityService.
/// </summary>
public sealed class EntraEmployeeClassificationClient : IEntraEmployeeClassificationClient
{
    private const string GraphBaseUrl = "https://graph.microsoft.com/v1.0";
    private const string GraphScope = "https://graph.microsoft.com/.default";

    // Graph requires this type annotation on the attribute-set object of every custom
    // security attribute write; without it the PATCH is rejected as untyped.
    private const string AttributeValueODataType =
        "#Microsoft.DirectoryServices.CustomSecurityAttributeValue";

    private readonly HttpClient _httpClient;
    private readonly EntraClassificationSettings _settings;

    // MSAL and Azure.Identity both cache tokens per credential instance, so these are
    // built once and reused. Rebuilding per request (as the older Graph clients here do)
    // throws that cache away and re-authenticates on every call.
    private readonly Lazy<IConfidentialClientApplication?> _confidentialClient;
    private readonly Lazy<TokenCredential?> _managedIdentityCredential;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public EntraEmployeeClassificationClient(
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

    private string PrimaryDefinitionId => $"{_settings.AttributeSet}_{_settings.PrimaryAttributeName}";

    private string SecondaryDefinitionId => $"{_settings.AttributeSet}_{_settings.SecondaryAttributeName}";

    private string ProfessionDefinitionId => $"{_settings.AttributeSet}_{_settings.ProfessionAttributeName}";

    public async Task<EntraEmployeeClassification?> GetAsync(
        string userPrincipalName,
        CancellationToken cancellationToken = default)
    {
        ValidateSettings();

        var url = $"{GraphBaseUrl}/users/{Uri.EscapeDataString(userPrincipalName)}" +
                  "?$select=id,userPrincipalName,displayName,customSecurityAttributes";

        using var response = await SendAsync(HttpMethod.Get, url, content: null, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        await EnsureSuccessAsync(response, $"read classification for {userPrincipalName}", cancellationToken);

        var node = await ReadJsonAsync(response, cancellationToken);

        return node is null ? null : ParseClassification(node);
    }

    public async Task<IReadOnlyList<EntraEmployeeClassification>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        ValidateSettings();

        var results = new List<EntraEmployeeClassification>();

        var url = $"{GraphBaseUrl}/users" +
                  "?$select=id,userPrincipalName,displayName,customSecurityAttributes" +
                  "&$top=999";

        while (!string.IsNullOrWhiteSpace(url))
        {
            using var response = await SendAsync(HttpMethod.Get, url, content: null, cancellationToken);

            await EnsureSuccessAsync(response, "list classifications", cancellationToken);

            var node = await ReadJsonAsync(response, cancellationToken);

            if (node?["value"] is JsonArray items)
            {
                foreach (var item in items)
                {
                    if (item is JsonObject user)
                    {
                        results.Add(ParseClassification(user));
                    }
                }
            }

            url = node?["@odata.nextLink"]?.GetValue<string>();
        }

        return results;
    }

    public async Task<EntraEmployeeClassification> UpdateCustomSecurityAttributesAsync(
        string userPrincipalName,
        UpdateEmployeeClassificationRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateSettings();

        var attributeSet = new JsonObject
        {
            ["@odata.type"] = AttributeValueODataType,

            // A single-valued attribute is cleared by assigning null, a multivalued one by
            // assigning an empty collection. Both are always written rather than omitted,
            // so clearing a value in the UI actually removes it in Entra instead of
            // silently leaving the previous assignment in place.
            [_settings.PrimaryAttributeName] = string.IsNullOrWhiteSpace(request.PrimaryFaglighed)
                ? null
                : JsonValue.Create(request.PrimaryFaglighed.Trim()),

            [$"{_settings.SecondaryAttributeName}@odata.type"] = "#Collection(String)",
            [_settings.SecondaryAttributeName] = new JsonArray(
                request.SecondaryFagligheder
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(x => (JsonNode)JsonValue.Create(x.Trim())!)
                    .ToArray()),

            [_settings.ProfessionAttributeName] = string.IsNullOrWhiteSpace(request.Profession)
                ? null
                : JsonValue.Create(request.Profession.Trim())
        };

        var payload = new JsonObject
        {
            ["customSecurityAttributes"] = new JsonObject
            {
                [_settings.AttributeSet] = attributeSet
            }
        };

        using (var response = await SendAsync(
                   HttpMethod.Patch,
                   $"{GraphBaseUrl}/users/{Uri.EscapeDataString(userPrincipalName)}",
                   payload,
                   cancellationToken))
        {
            await EnsureSuccessAsync(
                response,
                $"update classification for {userPrincipalName}",
                cancellationToken);
        }

        // Re-read rather than echoing the request back: Entra is the authority, and this is
        // what makes "what the UI shows" equal "what Entra stored" even if a value was
        // normalized or an assignment silently ignored.
        var stored = await GetAsync(userPrincipalName, cancellationToken)
                     ?? throw new InvalidOperationException(
                         $"Classification for {userPrincipalName} could not be read back after update.");

        return stored;
    }

    public async Task WriteExtensionAttributeMirrorAsync(
        string userPrincipalName,
        EntraEmployeeClassification classification,
        CancellationToken cancellationToken = default)
    {
        ValidateSettings();

        var mirror = BuildExtensionAttributeMirror(classification.SecondaryFagligheder);

        // Cleared with an empty string, NOT null. Graph accepts a null extensionAttribute
        // with 204 No Content and then leaves the previous value in place — verified
        // against this tenant: PATCH {"extensionAttribute2": null} returned 204 and a
        // re-read still showed the old value, while "" cleared it.
        //
        // This matters more than it looks: these mirrors drive Entra dynamic group
        // membership. Silently keeping a stale value means someone who has had a faglighed
        // removed stays in the group it grants, indefinitely.
        var payload = new JsonObject
        {
            ["onPremisesExtensionAttributes"] = new JsonObject
            {
                ["extensionAttribute1"] = JsonValue.Create(classification.PrimaryFaglighed ?? string.Empty),
                ["extensionAttribute2"] = JsonValue.Create(mirror ?? string.Empty),
                ["extensionAttribute3"] = JsonValue.Create(classification.Profession ?? string.Empty)
            }
        };

        using var response = await SendAsync(
            HttpMethod.Patch,
            $"{GraphBaseUrl}/users/{Uri.EscapeDataString(userPrincipalName)}",
            payload,
            cancellationToken);

        await EnsureSuccessAsync(
            response,
            $"mirror classification to extension attributes for {userPrincipalName}",
            cancellationToken);
    }

    /// <summary>
    /// Serializes multiple values into the pipe-delimited form Entra dynamic membership
    /// rules can match with -contains, e.g. ["El","IKT og BIM"] becomes "|El|IKT og BIM|".
    /// The leading and trailing pipes matter: they make "|El|" an unambiguous match that
    /// cannot also hit a longer value merely starting with "El".
    /// Returns null for an empty list, so the attribute is cleared rather than set to "||".
    /// </summary>
    public static string? BuildExtensionAttributeMirror(IReadOnlyList<string> values)
    {
        var cleaned = values
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .ToList();

        return cleaned.Count == 0 ? null : $"|{string.Join("|", cleaned)}|";
    }

    public async Task<EmployeeClassificationOptionsDto> GetAllowedValuesAsync(
        CancellationToken cancellationToken = default)
    {
        ValidateSettings();

        var primary = await GetAllowedValuesAsync(PrimaryDefinitionId, cancellationToken);
        var secondary = await GetAllowedValuesAsync(SecondaryDefinitionId, cancellationToken);
        var professions = await GetAllowedValuesAsync(ProfessionDefinitionId, cancellationToken);

        return new EmployeeClassificationOptionsDto
        {
            PrimaryFagligheder = primary,
            SecondaryFagligheder = secondary,
            Professions = professions
        };
    }

    private async Task<IReadOnlyList<string>> GetAllowedValuesAsync(
        string definitionId,
        CancellationToken cancellationToken)
    {
        var url = $"{GraphBaseUrl}/directory/customSecurityAttributeDefinitions/{definitionId}/allowedValues";

        using var response = await SendAsync(HttpMethod.Get, url, content: null, cancellationToken);

        await EnsureSuccessAsync(response, $"read allowed values for {definitionId}", cancellationToken);

        var node = await ReadJsonAsync(response, cancellationToken);

        var values = new List<string>();

        if (node?["value"] is JsonArray items)
        {
            foreach (var item in items)
            {
                // Deactivated values stay in the definition forever (Entra has no delete for
                // predefined values) but must not be offered as new assignments.
                if (item?["isActive"]?.GetValue<bool>() == false)
                {
                    continue;
                }

                var id = item?["id"]?.GetValue<string>();

                if (!string.IsNullOrWhiteSpace(id))
                {
                    values.Add(id);
                }
            }
        }

        return values
            .OrderBy(x => x, StringComparer.CurrentCulture)
            .ToList();
    }

    private EntraEmployeeClassification ParseClassification(JsonObject user)
    {
        var attributeSet = user["customSecurityAttributes"]?[_settings.AttributeSet];

        var secondary = new List<string>();

        if (attributeSet?[_settings.SecondaryAttributeName] is JsonArray secondaryValues)
        {
            foreach (var value in secondaryValues)
            {
                var text = value?.GetValue<string>();

                if (!string.IsNullOrWhiteSpace(text))
                {
                    secondary.Add(text);
                }
            }
        }

        return new EntraEmployeeClassification
        {
            UserId = user["id"]?.GetValue<string>(),
            UserPrincipalName = user["userPrincipalName"]?.GetValue<string>() ?? string.Empty,
            PrimaryFaglighed = attributeSet?[_settings.PrimaryAttributeName]?.GetValue<string>(),
            SecondaryFagligheder = secondary,
            Profession = attributeSet?[_settings.ProfessionAttributeName]?.GetValue<string>()
        };
    }

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

    private static async Task<JsonObject?> ReadJsonAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);

        return await JsonSerializer.DeserializeAsync<JsonObject>(stream, JsonOptions, cancellationToken);
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

        // 403 here is nearly always a missing Graph application permission rather than a bad
        // request, and the raw Graph message says little — naming the permissions makes the
        // cause obvious from the log instead of needing a Graph round-trip to diagnose.
        var hint = response.StatusCode == HttpStatusCode.Forbidden
            ? " The app registration likely lacks one of: CustomSecAttributeAssignment.ReadWrite.All, " +
              "CustomSecAttributeDefinition.Read.All, User.ReadWrite.All (application permissions, admin consent granted)."
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
                             "No Entra credential is configured for employee classification.");

        var token = await credential.GetTokenAsync(
            new TokenRequestContext([GraphScope]),
            cancellationToken);

        return token.Token;
    }

    private void ValidateSettings()
    {
        if (string.IsNullOrWhiteSpace(_settings.TenantId))
        {
            throw new InvalidOperationException("EntraClassification:TenantId is missing.");
        }

        if (string.IsNullOrWhiteSpace(_settings.AttributeSet))
        {
            throw new InvalidOperationException("EntraClassification:AttributeSet is missing.");
        }

        // ClientId is required for the client-secret flow and for a user-assigned managed
        // identity, but a system-assigned identity needs neither, so it is not required here.
        if (!string.IsNullOrWhiteSpace(_settings.ClientSecret) &&
            string.IsNullOrWhiteSpace(_settings.ClientId))
        {
            throw new InvalidOperationException(
                "EntraClassification:ClientId is required when a ClientSecret is configured.");
        }
    }
}
