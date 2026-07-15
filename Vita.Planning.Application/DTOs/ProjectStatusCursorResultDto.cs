using System.Text.Json.Serialization;

namespace Vita.Planning.Application.DTOs;

public sealed class ProjectStatusCursorResultDto
{
    [JsonPropertyName("cursor")]
    public string? Cursor { get; set; }

    [JsonPropertyName("items")]
    public List<SourceEconomicProjectStatusDto> Items { get; set; } = new();
}