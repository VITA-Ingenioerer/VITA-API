using System.Text.Json.Serialization;

namespace Vita.Atlas.Application.DTOs;

public sealed class ActivityCursorResultDto
{
    [JsonPropertyName("cursor")]
    public string? Cursor { get; set; }

    [JsonPropertyName("items")]
    public List<SourceEconomicActivityDto> Items { get; set; } = new();
}