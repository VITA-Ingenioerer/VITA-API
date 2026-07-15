using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Vita.Planning.Application.DTOs;
using Vita.Planning.Application.Exceptions;
using Vita.Planning.Application.Interfaces;

namespace Vita.Planning.Infrastructure.Clients;

public sealed class EconomicTimeEntryClient : IEconomicTimeEntryClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    // Danish wording matches e-conomic's own UI so users see the same message there and in Vita.Planning.
    private static readonly Dictionary<string, (string Title, string Detail)> KnownApprovalErrors = new()
    {
        ["TimeEntryCannotBeCreatedUpdatedOrApprovedAccountingPeriodIsBarredOrClosed"] =
            ("Kan ikke godkende tidsregistreringer", "En eller flere tidsregistreringer ligger i en spærret regnskabsperiode.")
    };

    private readonly HttpClient _httpClient;
    private readonly EconomicSettings _settings;
    private readonly ILogger<EconomicTimeEntryClient> _logger;

    public EconomicTimeEntryClient(
        HttpClient httpClient,
        IOptions<EconomicSettings> settings,
        ILogger<EconomicTimeEntryClient> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;
    }

    public Task<IReadOnlyList<EconomicTimeEntryDto>> GetTimeEntriesAsync(
        int employeeNumber,
        DateTime fromDate,
        DateTime? toDate = null,
        CancellationToken cancellationToken = default)
    {
        var filter = BuildDateRangeFilter($"employeeNumber$eq:{employeeNumber}", fromDate, toDate);
        return FetchByFilterAsync(filter, cancellationToken);
    }

    public Task<IReadOnlyList<EconomicTimeEntryDto>> GetTimeEntriesByProjectAsync(
        int projectNumber,
        DateTime fromDate,
        DateTime? toDate = null,
        int? employeeNumber = null,
        CancellationToken cancellationToken = default)
    {
        var baseFilter = employeeNumber.HasValue
            ? $"projectNumber$eq:{projectNumber}$and:employeeNumber$eq:{employeeNumber.Value}"
            : $"projectNumber$eq:{projectNumber}";
        var filter = BuildDateRangeFilter(baseFilter, fromDate, toDate);
        return FetchByFilterAsync(filter, cancellationToken);
    }

    private static string BuildDateRangeFilter(string baseFilter, DateTime fromDate, DateTime? toDate)
    {
        var fromUtc = NormalizeFilterDate(fromDate, isEndOfDay: false);
        var filter = $"{baseFilter}$and:date$gte:{fromUtc:yyyy-MM-ddTHH:mm:ssZ}";

        if (toDate.HasValue)
        {
            var toUtc = NormalizeFilterDate(toDate.Value, isEndOfDay: true);
            filter += $"$and:date$lte:{toUtc:yyyy-MM-ddTHH:mm:ssZ}";
        }

        return filter;
    }

    private async Task<IReadOnlyList<EconomicTimeEntryDto>> FetchByFilterAsync(string filter, CancellationToken cancellationToken)
    {
        var all = new List<EconomicTimeEntryDto>();
        string? cursor = null;
        var encodedFilter = Uri.EscapeDataString(filter);

        do
        {
            var url = string.IsNullOrWhiteSpace(cursor)
                ? $"TimeEntries?filter={encodedFilter}"
                : $"TimeEntries?filter={encodedFilter}" +
                  $"&cursor={Uri.EscapeDataString(cursor)}";

            using var request = BuildGet(url);
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var page = await JsonSerializer.DeserializeAsync<TimeEntryCursorResult>(stream, JsonOptions, cancellationToken);

            if (page?.Items is { Count: > 0 })
            {
                all.AddRange(page.Items);
            }

            cursor = page?.Cursor;

        } while (!string.IsNullOrWhiteSpace(cursor));

        return all;
    }

    private static DateTime NormalizeFilterDate(DateTime value, bool isEndOfDay)
    {
        var utcValue = value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };

        return isEndOfDay
            ? new DateTime(utcValue.Year, utcValue.Month, utcValue.Day, 23, 59, 59, DateTimeKind.Utc)
            : new DateTime(utcValue.Year, utcValue.Month, utcValue.Day, 0, 0, 0, DateTimeKind.Utc);
    }

    public async Task<EconomicTimeEntryDto?> GetTimeEntryAsync(int number, CancellationToken cancellationToken = default)
    {
        using var request = BuildGet($"TimeEntries/{number}");
        using var response = await _httpClient.SendAsync(request, cancellationToken);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonSerializer.DeserializeAsync<EconomicTimeEntryDto>(stream, JsonOptions, cancellationToken);
    }

    public async Task<int> CreateTimeEntryAsync(CreateTimeEntryRequest request, CancellationToken cancellationToken = default)
    {
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "TimeEntries");
        AddAuthHeaders(httpRequest);
        httpRequest.Content = JsonContent.Create(new
        {
            projectNumber = request.ProjectNumber,
            activityNumber = request.ActivityNumber,
            employeeNumber = request.EmployeeNumber,
            date = request.Date,
            numberOfHours = request.NumberOfHours,
            text = request.Text
        });

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var created = await JsonSerializer.DeserializeAsync<CreatedTimeEntryResult>(stream, JsonOptions, cancellationToken);
        return created?.Number ?? 0;
    }

    public async Task UpdateTimeEntryAsync(UpdateTimeEntryRequest request, CancellationToken cancellationToken = default)
    {
        var payload = new Dictionary<string, object?>
        {
            ["number"] = request.Number,
            ["projectNumber"] = request.ProjectNumber,
            ["activityNumber"] = request.ActivityNumber,
            ["employeeNumber"] = request.EmployeeNumber,
            ["date"] = request.Date,
            ["numberOfHours"] = request.NumberOfHours,
            ["text"] = request.Text,
            ["objectVersion"] = request.ObjectVersion
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Put, "TimeEntries");
        AddAuthHeaders(httpRequest);
        httpRequest.Content = JsonContent.Create(payload);

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task ApproveTimeEntriesAsync(
        IReadOnlyList<int> numbers,
        int? bookOn = null,
        CancellationToken cancellationToken = default)
    {
        if (numbers.Count == 0)
        {
            return;
        }

        var payload = new Dictionary<string, object?>
        {
            ["numbers"] = numbers
        };

        if (bookOn.HasValue)
        {
            payload["bookOn"] = bookOn.Value;
        }

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "TimeEntries/approve");
        AddAuthHeaders(httpRequest);
        httpRequest.Content = JsonContent.Create(payload);

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError(
                "e-conomic TimeEntries/approve failed with {StatusCode}. Request: {Request}. Response: {ResponseBody}",
                (int)response.StatusCode,
                JsonSerializer.Serialize(payload),
                errorBody);

            throw BuildApprovalException(errorBody, response.StatusCode);
        }
    }

    private static EconomicApprovalException BuildApprovalException(string errorBody, System.Net.HttpStatusCode statusCode)
    {
        var (errorCode, detail) = ParseEconomicError(errorBody);

        if (errorCode is not null && KnownApprovalErrors.TryGetValue(errorCode, out var known))
        {
            return new EconomicApprovalException(known.Title, known.Detail, errorCode, statusCode);
        }

        var fallbackDetail = detail
            ?? (string.IsNullOrWhiteSpace(errorBody) ? $"e-conomic returned {(int)statusCode} {statusCode}." : errorBody);

        return new EconomicApprovalException("Kan ikke godkende tidsregistreringer", fallbackDetail, errorCode ?? "Unknown", statusCode);
    }

    private static (string? ErrorCode, string? Detail) ParseEconomicError(string errorBody)
    {
        try
        {
            using var doc = JsonDocument.Parse(errorBody);
            var root = doc.RootElement;

            string? errorCode = root.TryGetProperty("errorCode", out var errorCodeProp)
                ? errorCodeProp.GetString()
                : null;

            string? detail = null;

            if (root.TryGetProperty("errors", out var errorsProp) && errorsProp.ValueKind == JsonValueKind.Array)
            {
                var first = errorsProp.EnumerateArray().FirstOrDefault();

                if (first.ValueKind == JsonValueKind.Object)
                {
                    errorCode ??= first.TryGetProperty("errorCode", out var itemCode) ? itemCode.GetString() : null;
                    detail = first.TryGetProperty("message", out var itemMessage) ? itemMessage.GetString() : null;
                }
            }

            detail ??= root.TryGetProperty("detail", out var detailProp) ? detailProp.GetString() : null;

            return (errorCode, detail);
        }
        catch (JsonException)
        {
            return (null, null);
        }
    }

    public async Task DeleteTimeEntryAsync(int number, CancellationToken cancellationToken = default)
    {
        using var httpRequest = new HttpRequestMessage(HttpMethod.Delete, $"TimeEntries/{number}");
        AddAuthHeaders(httpRequest);

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private HttpRequestMessage BuildGet(string url)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        AddAuthHeaders(request);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return request;
    }

    private void AddAuthHeaders(HttpRequestMessage request)
    {
        request.Headers.Add("X-AppSecretToken", _settings.AppSecretToken);
        request.Headers.Add("X-AgreementGrantToken", _settings.AgreementGrantToken);
    }

    private sealed class TimeEntryCursorResult
    {
        public string? Cursor { get; set; }
        public List<EconomicTimeEntryDto> Items { get; set; } = [];
    }

    private sealed class CreatedTimeEntryResult
    {
        public int Number { get; set; }
    }
}
