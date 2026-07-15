using System.Text.Json.Serialization;

namespace Vita.Planning.Application.DTOs;

public sealed class ProjectGroupCursorResultDto
{
    [JsonPropertyName("cursor")]
    public string? Cursor { get; set; }

    [JsonPropertyName("items")]
    public List<SourceEconomicProjectGroupDto> Items { get; set; } = new();
}