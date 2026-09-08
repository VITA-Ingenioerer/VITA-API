using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Vita.Planning.Application.DTOs;
using Vita.Planning.Application.Interfaces;

namespace Vita.Planning.Infrastructure.Clients;

/// <summary>
/// Calls the Danish address API (DAWA) at api.dataforsyningen.dk.
/// No authentication required — free public API.
/// </summary>
public sealed class DawaService : IDawaService
{
    private readonly HttpClient _httpClient;
    private const string BaseUrl = "https://api.dataforsyningen.dk";

    public DawaService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    // Queries adgangsadresser (access/building-level addresses), not adresser
    // (unit-level, one entry per apartment/floor/door). A project or offer is
    // about a building or site, never a specific unit inside it — searching
    // adresser meant a multi-unit building could only ever be picked by first
    // choosing one arbitrary apartment, with no way to select the building as
    // a whole. adgangsadresser has exactly one entry per building/entrance,
    // which is also what makes ProjectDawaId a reliable duplicate-detection
    // key: two offers on different units of the same building now resolve to
    // the same id instead of two different ones.
    public async Task<IReadOnlyList<DawaAddressSearchResultDto>> SearchAsync(string query, string? postalCode = null, string? regionCode = null, CancellationToken cancellationToken = default)
    {
        var url = $"{BaseUrl}/adgangsadresser/autocomplete?q={Uri.EscapeDataString(query)}&per_side=20&fuzzy=";

        if (!string.IsNullOrWhiteSpace(postalCode))
            url += $"&postnr={Uri.EscapeDataString(postalCode.Trim())}";

        if (!string.IsNullOrWhiteSpace(regionCode))
            url += $"&regionskode={Uri.EscapeDataString(regionCode.Trim())}";

        var items = await _httpClient.GetFromJsonAsync<List<DawaAutocompleteItem>>(url, cancellationToken);

        return items?
            .Where(x => x.Adgangsadresse is not null)
            .Select(x => Map(x))
            .ToList() ?? [];
    }

    public async Task<IReadOnlyList<DawaPostalCodeDto>> SearchPostalCodesAsync(string query, CancellationToken cancellationToken = default)
    {
        var url = $"{BaseUrl}/postnumre/autocomplete?q={Uri.EscapeDataString(query)}&per_side=20";

        var items = await _httpClient.GetFromJsonAsync<List<DawaPostalCodeAutocompleteItem>>(url, cancellationToken);

        return items?
            .Where(x => x.Postnummer is not null)
            .Select(x => new DawaPostalCodeDto
            {
                PostalCode = x.Postnummer!.Nr ?? string.Empty,
                Name = x.Postnummer.Navn ?? string.Empty,
                DisplayText = x.Tekst ?? string.Empty
            })
            .ToList() ?? [];
    }

    public async Task<IReadOnlyList<DawaRegionDto>> GetRegionsAsync(CancellationToken cancellationToken = default)
    {
        var url = $"{BaseUrl}/regioner?fields=kode,navn";

        var items = await _httpClient.GetFromJsonAsync<List<DawaRegionItem>>(url, cancellationToken);

        return items?
            .Select(x => new DawaRegionDto
            {
                RegionCode = x.Kode ?? string.Empty,
                Name = x.Navn ?? string.Empty
            })
            .OrderBy(x => x.Name)
            .ToList() ?? [];
    }

    private static DawaAddressSearchResultDto Map(DawaAutocompleteItem item)
    {
        var a = item.Adgangsadresse!;
        var street = string.IsNullOrWhiteSpace(a.Husnr)
            ? a.Vejnavn ?? string.Empty
            : $"{a.Vejnavn} {a.Husnr}".Trim();

        return new DawaAddressSearchResultDto
        {
            Id = a.Id,
            DisplayText = item.Tekst,
            StreetAddress = street,
            PostalCode = a.Postnr ?? string.Empty,
            City = a.Postnrnavn ?? string.Empty,
            Municipality = a.Kommunenavn,
            Region = a.Regionsnavn,
        };
    }

    private sealed class DawaPostalCodeAutocompleteItem
    {
        [JsonPropertyName("tekst")]
        public string? Tekst { get; set; }

        [JsonPropertyName("postnummer")]
        public DawaPostnummer? Postnummer { get; set; }
    }

    private sealed class DawaPostnummer
    {
        [JsonPropertyName("nr")]
        public string? Nr { get; set; }

        [JsonPropertyName("navn")]
        public string? Navn { get; set; }
    }

    private sealed class DawaRegionItem
    {
        [JsonPropertyName("kode")]
        public string? Kode { get; set; }

        [JsonPropertyName("navn")]
        public string? Navn { get; set; }
    }

    private sealed class DawaAutocompleteItem
    {
        [JsonPropertyName("tekst")]
        public string Tekst { get; set; } = string.Empty;

        [JsonPropertyName("adgangsadresse")]
        public DawaAdgangsadresse? Adgangsadresse { get; set; }
    }

    // Building/entrance-level fields only — adgangsadresser has no etage/dør,
    // since floor and door only exist at the individual-unit (adresse) level.
    private sealed class DawaAdgangsadresse
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("vejnavn")]
        public string? Vejnavn { get; set; }

        [JsonPropertyName("husnr")]
        public string? Husnr { get; set; }

        [JsonPropertyName("postnr")]
        public string? Postnr { get; set; }

        [JsonPropertyName("postnrnavn")]
        public string? Postnrnavn { get; set; }

        [JsonPropertyName("kommunenavn")]
        public string? Kommunenavn { get; set; }

        [JsonPropertyName("regionsnavn")]
        public string? Regionsnavn { get; set; }
    }
}
