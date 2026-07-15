using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Vita.Planning.Application.DTOs;
using Vita.Planning.Application.Exceptions;
using Vita.Planning.Application.Interfaces;

namespace Vita.Planning.Infrastructure.Clients;

public sealed class EconomicProjectWriteClient : IEconomicProjectWriteClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly EconomicSettings _settings;

    public EconomicProjectWriteClient(
        HttpClient httpClient,
        IOptions<EconomicSettings> settings)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
    }

    public async Task CreateProjectAsync(
        int projectNumber,
        string name,
        int projectGroupNumber,
        int? customerNumber,
        int? responsibleEmployeeNumber,
        bool isMainProject,
        int? mainProjectNumber,
        CancellationToken cancellationToken = default)
    {
        // e-conomic Projects API uses flat fields — number, customerNumber, etc. are top-level ints.
        // mainProjectnumber uses a lowercase 'n' — this matches the actual API contract.
        var payload = new Dictionary<string, object?>
        {
            ["number"] = projectNumber,
            ["name"] = name,
            ["projectGroupNumber"] = projectGroupNumber,
            ["isMainProject"] = isMainProject,
            ["isMileageInvoiced"] = true
        };

        if (customerNumber.HasValue)
            payload["customerNumber"] = customerNumber.Value;

        if (responsibleEmployeeNumber.HasValue)
            payload["responsibleEmployeeNumber"] = responsibleEmployeeNumber.Value;

        if (!isMainProject && mainProjectNumber.HasValue)
            payload["mainProjectnumber"] = mainProjectNumber.Value;

        using var request = new HttpRequestMessage(HttpMethod.Post, "Projects");
        AddAuthHeaders(request);
        request.Content = JsonContent.Create(payload);

        using var response = await _httpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var errorCode = TryParseEconomicErrorCode(body);

            if (errorCode == "ProjectCannotBeCreatedUpdatedNumberAlreadyExists")
                throw new EconomicProjectNumberConflictException(projectNumber);

            throw new InvalidOperationException(
                $"e-conomic returned {(int)response.StatusCode} when creating project {projectNumber}: {body}");
        }
    }

    private static string? TryParseEconomicErrorCode(string body)
    {
        try
        {
            var error = JsonSerializer.Deserialize<EconomicErrorResponseDto>(body, JsonOptions);
            return error?.ErrorCode ?? error?.Errors?.FirstOrDefault()?.ErrorCode;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private void AddAuthHeaders(HttpRequestMessage request)
    {
        request.Headers.Add("X-AppSecretToken", _settings.AppSecretToken);
        request.Headers.Add("X-AgreementGrantToken", _settings.AgreementGrantToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }
}
