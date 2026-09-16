namespace Vita.Atlas.Application;

public static class PlanningDefaults
{
    /// <summary>
    /// What a missing probability means. Nothing stated is "certain", not "unknown" — a case
    /// nobody has put a percentage on is planned at full weight, and every screen shows it as
    /// 100%. Null never reaches a caller: read paths coalesce, write paths persist the 100,
    /// and Sql/2026-09-default-probability-to-100.sql clears the rows that predate that rule.
    ///
    /// An explicit 0 is a stated probability and is left alone.
    /// </summary>
    public const decimal ProbabilityPercent = 100m;
}
