using System.Text.Json;
using Vita.Planning.Application.DTOs;
using Vita.Planning.Application.Interfaces;

namespace Vita.Planning.Infrastructure.Services;

public sealed class InternalUserSourceClient : IInternalUserSourceClient
{
    private readonly HttpClient _httpClient;

    public InternalUserSourceClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IReadOnlyList<SourceUserDto>> GetUsersAsync(CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.GetAsync("api/users/", cancellationToken);

        response.EnsureSuccessStatusCode();

        var stream = await response.Content.ReadAsStreamAsync(cancellationToken);

        var users = await JsonSerializer.DeserializeAsync<List<SourceUserDto>>(
            stream,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            },
            cancellationToken);

        return users ?? new List<SourceUserDto>();
    }
}