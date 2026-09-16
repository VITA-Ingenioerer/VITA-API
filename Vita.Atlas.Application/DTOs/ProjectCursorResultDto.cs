using System.Text.Json.Serialization;

namespace Vita.Atlas.Application.DTOs;

public sealed class ProjectCursorResultDto
{
    [JsonPropertyName("cursor")]
    public string? Cursor { get; set; }

    [JsonPropertyName("items")]
    public List<SourceEconomicProjectDto> Items { get; set; } = new();
}