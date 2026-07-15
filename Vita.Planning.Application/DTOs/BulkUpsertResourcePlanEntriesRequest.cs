using System.ComponentModel.DataAnnotations;

namespace Vita.Planning.Application.DTOs;

public sealed class BulkUpsertResourcePlanEntriesRequest
{
    [Required]
    [MinLength(1)]
    public IReadOnlyList<BulkResourcePlanEntryItemRequest> Entries { get; set; } = Array.Empty<BulkResourcePlanEntryItemRequest>();
}

public sealed class BulkResourcePlanEntryItemRequest
{
    public int? ResourcePlanEntryId { get; set; }

    [Required]
    public int ResourcePlanId { get; set; }

    [Required]
    public DateOnly PlanDate { get; set; }

    [Required]
    public decimal Hours { get; set; }

    [MaxLength(510)]
    public string? Description { get; set; }

    public bool IsManualOverride { get; set; }

    [MaxLength(200)]
    public string? ChangedBy { get; set; }

    [Required]
    public int PlanningTargetId { get; set; }
}
