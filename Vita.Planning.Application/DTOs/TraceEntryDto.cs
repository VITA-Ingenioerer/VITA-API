using System.Text.Json.Serialization;

namespace Vita.Planning.Application.DTOs;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TraceSource
{
    BusinessEvent,
    Error,
    SyncRun,
    SyncError,
    LifecycleLog
}

public sealed class TraceEntryDto
{
    public DateTime TimestampUtc { get; set; }
    public TraceSource Source { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Severity { get; set; }
    public string? EntityType { get; set; }
    public string? EntityId { get; set; }
    public string? Actor { get; set; }
    public long SourceId { get; set; }
}

public sealed class CorrelationTraceDto
{
    public Guid CorrelationId { get; set; }
    public IReadOnlyList<TraceEntryDto> Entries { get; set; } = Array.Empty<TraceEntryDto>();
}
