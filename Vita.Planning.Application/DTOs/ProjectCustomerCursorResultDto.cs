using System.Text.Json.Serialization;

namespace Vita.Planning.Application.DTOs;

public sealed class ProjectCustomerCursorResultDto
{
    [JsonPropertyName("cursor")]
    public string? Cursor { get; set; }

    [JsonPropertyName("items")]
    public List<SourceEconomicProjectCustomerDto> Items { get; set; } = new();
}