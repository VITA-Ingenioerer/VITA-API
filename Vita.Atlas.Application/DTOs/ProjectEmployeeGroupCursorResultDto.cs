using System.Text.Json.Serialization;

namespace Vita.Atlas.Application.DTOs;

public sealed class ProjectEmployeeGroupCursorResultDto
{
    [JsonPropertyName("cursor")]
    public string? Cursor { get; set; }

    [JsonPropertyName("items")]
    public List<SourceEconomicProjectEmployeeGroupDto> Items { get; set; } = new();
}