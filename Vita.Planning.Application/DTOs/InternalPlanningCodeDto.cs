namespace Vita.Planning.Application.DTOs;

public sealed class InternalPlanningCodeDto
{
    public int InternalPlanningCodeId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string? OfficeCode { get; set; }
    public string? DefaultDescription { get; set; }
    public string? ColorTag { get; set; }
    public bool IsActive { get; set; }
    public bool IsPlannable { get; set; }
    public bool IsAbsence { get; set; }
    public bool IsInternal { get; set; }
    public bool IsBillable { get; set; }
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
}