using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Vita.Planning.Application.DTOs;
using Vita.Planning.Application.Interfaces;

namespace Vita.Planning.Infrastructure.Clients;

public sealed class EconomicProjectSourceClient : IEconomicProjectSourceClient
{
    private readonly HttpClient _httpClient;
    private readonly EconomicSettings _settings;

    public EconomicProjectSourceClient(
        HttpClient httpClient,
        IOptions<EconomicSettings> settings)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
    }

    public async Task<IReadOnlyList<SourceEconomicProjectDto>> GetProjectsAsync(CancellationToken cancellationToken = default)
    {
        var all = new List<SourceEconomicProjectDto>();
        string? cursor = null;

        do
        {
            var url = string.IsNullOrWhiteSpace(cursor)
                ? "Projects"
                : $"Projects?cursor={Uri.EscapeDataString(cursor)}";

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("X-AppSecretToken", _settings.AppSecretToken);
            request.Headers.Add("X-AgreementGrantToken", _settings.AgreementGrantToken);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);

            var page = await JsonSerializer.DeserializeAsync<ProjectCursorResultDto>(
                stream,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                },
                cancellationToken);

            if (page?.Items is { Count: > 0 })
            {
                all.AddRange(page.Items);
            }

            cursor = page?.Cursor;

        } while (!string.IsNullOrWhiteSpace(cursor));

        return all;
    }

    public async Task<SourceEconomicProjectDto?> GetProjectByNumberAsync(
        int projectNumber,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"Projects/{projectNumber}");
        request.Headers.Add("X-AppSecretToken", _settings.AppSecretToken);
        request.Headers.Add("X-AgreementGrantToken", _settings.AgreementGrantToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await _httpClient.SendAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);

        return await JsonSerializer.DeserializeAsync<SourceEconomicProjectDto>(
            stream,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            },
            cancellationToken);
    }
}