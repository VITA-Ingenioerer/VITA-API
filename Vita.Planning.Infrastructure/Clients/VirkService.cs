using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Vita.Planning.Application.DTOs;
using Vita.Planning.Application.Interfaces;

namespace Vita.Planning.Infrastructure.Clients;

/// <summary>
/// Calls the official Danish CVR / VIRK Elasticsearch API at distribution.virk.dk.
/// Requires a username and password from https://datacvr.virk.dk/artikel/vilkaar-og-betingelser
/// </summary>
public sealed class VirkService : IVirkService
{
    private readonly HttpClient _httpClient;
    private readonly VirkSettings _settings;

    private const string VirksomhedEndpoint = "/cvr-permanent/virksomhed/_search";

    public VirkService(HttpClient httpClient, IOptions<VirkSettings> settings)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
    }

    public async Task<IReadOnlyList<VirkCompanySearchDto>> SearchCompaniesAsync(string query, CancellationToken cancellationToken = default)
    {
        var body = new
        {
            query = new
            {
                match = new
                {
                    Vrvirksomhed_navne_navn = new { query, fuzziness = "AUTO" }
                }
            },
            _source = new[]
            {
                "Vrvirksomhed.cvrNummer",
                "Vrvirksomhed.navne",
                "Vrvirksomhed.beliggenhedsadresse",
                "Vrvirksomhed.virksomhedsstatus"
            },
            size = 20
        };

        var response = await PostSearchAsync(body, cancellationToken);

        return response?.Hits?.Hits?
            .Select(h => h.Source?.Vrvirksomhed)
            .Where(v => v is not null)
            .Select(v => new VirkCompanySearchDto
            {
                CvrNumber = v!.CvrNummer.ToString(),
                Name = v.ActiveName ?? string.Empty,
                City = v.ActiveAddress?.Postdistrikt,
                Status = v.ActiveStatus ?? string.Empty,
            })
            .ToList() ?? [];
    }

    public async Task<VirkCompanyDto?> GetCompanyAsync(string cvrNumber, CancellationToken cancellationToken = default)
    {
        if (!long.TryParse(cvrNumber, out var cvrLong))
        {
            return null;
        }

        var body = new
        {
            query = new
            {
                term = new Dictionary<string, object>
                {
                    ["Vrvirksomhed.cvrNummer"] = cvrLong
                }
            },
            size = 1
        };

        var response = await PostSearchAsync(body, cancellationToken);
        var v = response?.Hits?.Hits?.FirstOrDefault()?.Source?.Vrvirksomhed;
        if (v is null)
        {
            return null;
        }

        var addr = v.ActiveAddress;

        return new VirkCompanyDto
        {
            CvrNumber = v.CvrNummer.ToString(),
            Name = v.ActiveName ?? string.Empty,
            AddressLine = addr?.FullStreet,
            PostalCode = addr?.Postnummer?.ToString(),
            City = addr?.Postdistrikt,
            CountryCode = "DK",
            Status = v.ActiveStatus ?? string.Empty,
            IndustryCode = v.ActiveIndustry?.Branchekode,
            IndustryName = v.ActiveIndustry?.Branchetekst,
        };
    }

    private async Task<VirkSearchResponse?> PostSearchAsync(object body, CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"{_settings.BaseUrl}{VirksomhedEndpoint}")
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
        };

        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_settings.BrugerID}:{_settings.Password}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);

        var httpResponse = await _httpClient.SendAsync(request, cancellationToken);
        httpResponse.EnsureSuccessStatusCode();

        return await httpResponse.Content.ReadFromJsonAsync<VirkSearchResponse>(cancellationToken: cancellationToken);
    }

    // -------------------------------------------------------------------------
    // Response shape — maps the VIRK Elasticsearch JSON
    // -------------------------------------------------------------------------

    private sealed class VirkSearchResponse
    {
        [JsonPropertyName("hits")]
        public VirkHitsWrapper? Hits { get; set; }
    }

    private sealed class VirkHitsWrapper
    {
        [JsonPropertyName("hits")]
        public List<VirkHit>? Hits { get; set; }
    }

    private sealed class VirkHit
    {
        [JsonPropertyName("_source")]
        public VirkSource? Source { get; set; }
    }

    private sealed class VirkSource
    {
        [JsonPropertyName("Vrvirksomhed")]
        public VirkVirksomhed? Vrvirksomhed { get; set; }
    }

    private sealed class VirkVirksomhed
    {
        [JsonPropertyName("cvrNummer")]
        public long CvrNummer { get; set; }

        [JsonPropertyName("navne")]
        public List<VirkNavn>? Navne { get; set; }

        public string? ActiveName => Navne?
            .Where(n => n.Periode?.GyldigTil is null)
            .Select(n => n.Navn)
            .FirstOrDefault()
            ?? Navne?.LastOrDefault()?.Navn;

        [JsonPropertyName("beliggenhedsadresse")]
        public List<VirkAdresse>? Beliggenhedsadresse { get; set; }

        public VirkAdresse? ActiveAddress => Beliggenhedsadresse?
            .FirstOrDefault(a => a.Periode?.GyldigTil is null)
            ?? Beliggenhedsadresse?.LastOrDefault();

        [JsonPropertyName("virksomhedsstatus")]
        public List<VirkStatus>? Virksomhedsstatus { get; set; }

        public string? ActiveStatus => Virksomhedsstatus?
            .FirstOrDefault(s => s.Periode?.GyldigTil is null)?.Status
            ?? Virksomhedsstatus?.LastOrDefault()?.Status;

        [JsonPropertyName("hovedbranche")]
        public List<VirkBranche>? Hovedbranche { get; set; }

        public VirkBranche? ActiveIndustry => Hovedbranche?
            .FirstOrDefault(b => b.Periode?.GyldigTil is null)
            ?? Hovedbranche?.LastOrDefault();
    }

    private sealed class VirkNavn
    {
        [JsonPropertyName("navn")]
        public string? Navn { get; set; }

        [JsonPropertyName("periode")]
        public VirkPeriode? Periode { get; set; }
    }

    private sealed class VirkAdresse
    {
        [JsonPropertyName("vejnavn")]
        public string? Vejnavn { get; set; }

        [JsonPropertyName("husnummerFra")]
        public int? HusnummerFra { get; set; }

        [JsonPropertyName("postnummer")]
        public int? Postnummer { get; set; }

        [JsonPropertyName("postdistrikt")]
        public string? Postdistrikt { get; set; }

        [JsonPropertyName("periode")]
        public VirkPeriode? Periode { get; set; }

        public string? FullStreet => Vejnavn is null ? null
            : HusnummerFra.HasValue ? $"{Vejnavn} {HusnummerFra}" : Vejnavn;
    }

    private sealed class VirkStatus
    {
        [JsonPropertyName("status")]
        public string? Status { get; set; }

        [JsonPropertyName("periode")]
        public VirkPeriode? Periode { get; set; }
    }

    private sealed class VirkBranche
    {
        [JsonPropertyName("branchekode")]
        public string? Branchekode { get; set; }

        [JsonPropertyName("branchetekst")]
        public string? Branchetekst { get; set; }

        [JsonPropertyName("periode")]
        public VirkPeriode? Periode { get; set; }
    }

    private sealed class VirkPeriode
    {
        [JsonPropertyName("gyldigFra")]
        public string? GyldigFra { get; set; }

        [JsonPropertyName("gyldigTil")]
        public string? GyldigTil { get; set; }
    }
}
