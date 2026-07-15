namespace Vita.Planning.Application.DTOs;

public sealed class PlanningTargetDto
{
    public int PlanningTargetId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string TargetType { get; set; } = string.Empty;
    public int? ExtProjectNumber { get; set; }
    public int? OfferId { get; set; }
    public int? InternalPlanningCodeId { get; set; }
    public string? OfficeCode { get; set; }
    public bool IsActive { get; set; }
    public bool IsPlannable { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
}