using System.ComponentModel.DataAnnotations;

namespace Vita.Planning.Application.DTOs;

public sealed class CreateProjectLifecycleLogRequest
{
    [Required]
    [MaxLength(20)]
    public string TargetType { get; set; } = string.Empty;

    public int? ProjectNumber { get; set; }
    public int? OfferId { get; set; }

    [Required]
    [MaxLength(50)]
    public string EventType { get; set; } = string.Empty;

    [MaxLength(255)]
    public string? EventTitle { get; set; }

    public string? EventDescription { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public string? SnapshotJson { get; set; }

    [MaxLength(100)]
    public string? CreatedBy { get; set; }
}
