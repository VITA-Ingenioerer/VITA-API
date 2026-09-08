namespace Vita.Planning.Application.DTOs;

// Company-wide totals (the "total belastning" graph) never need individual
// entry rows — only, per day, how many hours landed against each planning
// target, summed across every employee. This is that sum, computed in SQL
// (GROUP BY plan_date, planning_target_id) instead of shipping every raw
// entry and summing client-side. Category/weighting logic stays client-side,
// keyed off PlanningTargetId against the already-loaded planning-target
// catalog, so there's only one place that logic lives.
public sealed class ResourcePlanEntrySummaryDto
{
    public DateOnly PlanDate { get; set; }
    public int PlanningTargetId { get; set; }
    public decimal Hours { get; set; }
}
