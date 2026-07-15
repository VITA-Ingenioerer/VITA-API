namespace Vita.Planning.Application.DTOs;

public sealed class OpsErrorDto
{
    public long OpsErrorId { get; set; }
    public string ExceptionType { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? StackTrace { get; set; }
    public string? RequestMethod { get; set; }
    public string? RequestPath { get; set; }
    public int? StatusCode { get; set; }
    public string? CallerUserId { get; set; }
    public string? CallerName { get; set; }
    public Guid CorrelationId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public sealed class RecordOpsErrorRequest
{
    public string ExceptionType { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? StackTrace { get; set; }
    public string? RequestMethod { get; set; }
    public string? RequestPath { get; set; }
    public int? StatusCode { get; set; }
    public string? CallerUserId { get; set; }
    public string? CallerName { get; set; }
    public Guid? CorrelationId { get; set; }
}
