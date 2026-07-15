using System.Text.Json.Serialization;

namespace Vita.Planning.Application.DTOs;

public sealed class ProjectActivityCursorResultDto
{
    [JsonPropertyName("cursor")]
    public string? Cursor { get; set; }

    [JsonPropertyName("items")]
    public List<SourceEconomicProjectActivityDto> Items { get; set; } = new();
}
