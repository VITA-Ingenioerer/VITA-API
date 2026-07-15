using Vita.Planning.Application.DTOs;
using Vita.Planning.Infrastructure.Data.Entities;

namespace Vita.Planning.Infrastructure.Services;

/// <summary>
/// Pure weekday-pattern resolution shared by capacity period generation
/// (CapacityImportController) and the daily capacity query service.
/// </summary>
public static class CapacityPatternCalculator
{
    /// <summary>
    /// Shifts every non-zero baseline weekday by (targetWeeklyHours - baseline.BaselineWeeklyHours) / workingDayCount,
    /// so e.g. a 37h baseline of 7.5/7.5/7.5/7.5/7 becomes 6.5/6.5/6.5/6.5/6 for a target of 32h.
    /// </summary>
    public static IReadOnlyDictionary<DayOfWeek, decimal> ComputeShiftedPattern(decimal targetWeeklyHours, CapacityDefaultsSettings baseline)
    {
        var baselinePattern = new Dictionary<DayOfWeek, decimal>
        {
            [DayOfWeek.Monday] = baseline.Monday,
            [DayOfWeek.Tuesday] = baseline.Tuesday,
            [DayOfWeek.Wednesday] = baseline.Wednesday,
            [DayOfWeek.Thursday] = baseline.Thursday,
            [DayOfWeek.Friday] = baseline.Friday,
            [DayOfWeek.Saturday] = baseline.Saturday,
            [DayOfWeek.Sunday] = baseline.Sunday,
        };

        var workingDays = baselinePattern.Where(kv => kv.Value > 0m).Select(kv => kv.Key).ToList();
        var delta = workingDays.Count > 0
            ? (targetWeeklyHours - baseline.BaselineWeeklyHours) / workingDays.Count
            : 0m;

        return Enum.GetValues<DayOfWeek>().ToDictionary(
            day => day,
            day => workingDays.Contains(day) ? Math.Max(0m, baselinePattern[day] + delta) : 0m);
    }

    /// <summary>
    /// A profile's explicit weekday columns win per-day; any unset day falls back to the
    /// shifted default pattern derived from the profile's (or fallback) weekly hours.
    /// </summary>
    public static IReadOnlyDictionary<DayOfWeek, decimal> ResolveProfileDailyHours(
        EmployeeCapacityProfile? profile, decimal weeklyHours, CapacityDefaultsSettings baseline)
    {
        var defaultPattern = ComputeShiftedPattern(weeklyHours, baseline);

        if (profile is null)
        {
            return defaultPattern;
        }

        var explicitPattern = GetExplicitDayValues(
            profile.MondayHours, profile.TuesdayHours, profile.WednesdayHours,
            profile.ThursdayHours, profile.FridayHours, profile.SaturdayHours, profile.SundayHours);

        return Enum.GetValues<DayOfWeek>().ToDictionary(
            day => day,
            day => explicitPattern[day] ?? defaultPattern[day]);
    }

    /// <summary>
    /// If the override has any explicit weekday column, those win per-day and unset days fall
    /// back to the profile's hours. Otherwise, the override's effective weekly hours
    /// (fixed_hours_per_week > weekly_hours > capacity_factor * profile weekly hours) are
    /// spread using the same shifted-pattern formula. An override with none of the above
    /// leaves the profile's hours untouched.
    /// </summary>
    public static IReadOnlyDictionary<DayOfWeek, decimal> ResolveOverrideDailyHours(
        EmployeeCapacityOverride? overrideRow,
        decimal profileWeeklyHours,
        IReadOnlyDictionary<DayOfWeek, decimal> profileDailyHours,
        CapacityDefaultsSettings baseline)
    {
        if (overrideRow is null)
        {
            return profileDailyHours;
        }

        var explicitPattern = GetExplicitDayValues(
            overrideRow.MondayHours, overrideRow.TuesdayHours, overrideRow.WednesdayHours,
            overrideRow.ThursdayHours, overrideRow.FridayHours, overrideRow.SaturdayHours, overrideRow.SundayHours);

        if (explicitPattern.Values.Any(v => v.HasValue))
        {
            return Enum.GetValues<DayOfWeek>().ToDictionary(
                day => day,
                day => explicitPattern[day] ?? profileDailyHours[day]);
        }

        decimal? effectiveWeekly = overrideRow.FixedHoursPerWeek
            ?? overrideRow.WeeklyHours
            ?? (overrideRow.CapacityFactor.HasValue ? profileWeeklyHours * overrideRow.CapacityFactor.Value : null);

        return effectiveWeekly.HasValue
            ? ComputeShiftedPattern(effectiveWeekly.Value, baseline)
            : profileDailyHours;
    }

    /// <summary>
    /// Picks the profile whose effective range covers <paramref name="date"/>, preferring the
    /// most recently started one when more than one somehow overlaps.
    /// </summary>
    public static EmployeeCapacityProfile? ResolveActiveProfile(IEnumerable<EmployeeCapacityProfile> profiles, DateOnly date) =>
        profiles
            .Where(p => p.EffectiveFrom <= date && (p.EffectiveTo == null || p.EffectiveTo >= date))
            .OrderByDescending(p => p.EffectiveFrom)
            .FirstOrDefault();

    /// <summary>
    /// Picks the override whose effective range covers <paramref name="date"/>, preferring the
    /// most recently started one when more than one somehow overlaps.
    /// </summary>
    public static EmployeeCapacityOverride? ResolveActiveOverride(IEnumerable<EmployeeCapacityOverride> overrides, DateOnly date) =>
        overrides
            .Where(o => o.EffectiveFrom <= date && (o.EffectiveTo == null || o.EffectiveTo >= date))
            .OrderByDescending(o => o.EffectiveFrom)
            .FirstOrDefault();

    private static Dictionary<DayOfWeek, decimal?> GetExplicitDayValues(
        decimal? monday, decimal? tuesday, decimal? wednesday, decimal? thursday,
        decimal? friday, decimal? saturday, decimal? sunday) => new()
    {
        [DayOfWeek.Monday] = monday,
        [DayOfWeek.Tuesday] = tuesday,
        [DayOfWeek.Wednesday] = wednesday,
        [DayOfWeek.Thursday] = thursday,
        [DayOfWeek.Friday] = friday,
        [DayOfWeek.Saturday] = saturday,
        [DayOfWeek.Sunday] = sunday,
    };
}
