using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Vita.Planning.Infrastructure.Data.Entities;

[Table("errors", Schema = "ops")]
public sealed class OpsError
{
    [Key]
    [Column("ops_error_id")]
    public long OpsErrorId { get; set; }

    [Column("exception_type")]
    public string ExceptionType { get; set; } = string.Empty;

    [Column("message")]
    public string Message { get; set; } = string.Empty;

    [Column("stack_trace")]
    public string? StackTrace { get; set; }

    [Column("request_method")]
    public string? RequestMethod { get; set; }

    [Column("request_path")]
    public string? RequestPath { get; set; }

    [Column("status_code")]
    public int? StatusCode { get; set; }

    [Column("caller_user_id")]
    public string? CallerUserId { get; set; }

    [Column("caller_name")]
    public string? CallerName { get; set; }

    [Column("correlation_id")]
    public Guid CorrelationId { get; set; }

    [Column("created_at_utc")]
    public DateTime CreatedAtUtc { get; set; }
}
