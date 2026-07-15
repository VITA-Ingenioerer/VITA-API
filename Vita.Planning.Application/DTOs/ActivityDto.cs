namespace Vita.Planning.Application.DTOs;

public sealed class ActivityDto
{
    /// <summary>
    /// ext.project_activities.number — only set when this activity came from a
    /// project-scoped lookup (e.g. GET /api/projects/{projectNumber}/activities). This, not
    /// ActivityNumber, is what resource_plan_entries.ProjectActivityId expects.
    /// </summary>
    public int? ProjectActivityId { get; set; }
    public int ActivityNumber { get; set; }
    public int ActivityGroupNumber { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal? CostPriceMarkupPercentage { get; set; }
    public DateTime? CutoffDate { get; set; }
    public bool? HideInSearch { get; set; }
    public int InLieuCode { get; set; }
    public bool? IsAccessible { get; set; }
    public decimal? SalesPriceAfter { get; set; }
    public decimal? SalesPriceBefore { get; set; }
    public string? ObjectVersion { get; set; }
    public DateTime SourceLastSyncedAt { get; set; }
}
