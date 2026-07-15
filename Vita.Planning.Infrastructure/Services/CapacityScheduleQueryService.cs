using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Vita.Planning.Application.DTOs;
using Vita.Planning.Application.Interfaces;
using Vita.Planning.Infrastructure.Data;
using Vita.Planning.Infrastructure.Data.Entities;

namespace Vita.Planning.Infrastructure.Services;

public sealed class CapacityScheduleQueryService : ICapacityScheduleQueryService
{
    private readonly PlanningDbContext _dbContext;
    private readonly CapacityDefaultsSettings _capacityDefaults;

    public CapacityScheduleQueryService(PlanningDbContext dbContext, IOptions<CapacityDefaultsSettings> capacityDefaults)
    {
        _dbContext = dbContext;
        _capacityDefaults = capacityDefaults.Value;
    }

    public async Task<IReadOnlyDictionary<DateOnly, decimal>> GetDefaultDailyHoursAsync(
        int employeeId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        var profiles = await LoadProfilesAsync(employeeId, from, to, cancellationToken);

        var result = new Dictionary<DateOnly, decimal>();

        for (var day = from; day <= to; day = day.AddDays(1))
        {
            var profile = CapacityPatternCalculator.ResolveActiveProfile(profiles, day);
            var weeklyHours = profile?.DefaultWeeklyHours ?? _capacityDefaults.BaselineWeeklyHours;
            var profileDailyHours = CapacityPatternCalculator.ResolveProfileDailyHours(profile, weeklyHours, _capacityDefaults);

            result[day] = profileDailyHours[day.DayOfWeek];
        }

        return result;
    }

    public async Task<IReadOnlyDictionary<DateOnly, decimal>> GetEffectiveDailyHoursAsync(
        int? employeeId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        // A virtual resource (no employeeId) has no capacity profile or override to look up;
        // it always gets the org-wide baseline weekly-hours schedule.
        var profiles = employeeId.HasValue
            ? await LoadProfilesAsync(employeeId.Value, from, to, cancellationToken)
            : [];

        var overrides = employeeId.HasValue
            ? await _dbContext.EmployeeCapacityOverrides
                .AsNoTracking()
                .Where(o => o.EmployeeId == employeeId.Value && o.IsActive &&
                            o.EffectiveFrom <= to && (o.EffectiveTo == null || o.EffectiveTo >= from))
                .ToListAsync(cancellationToken)
            : [];

        var result = new Dictionary<DateOnly, decimal>();

        for (var day = from; day <= to; day = day.AddDays(1))
        {
            var profile = CapacityPatternCalculator.ResolveActiveProfile(profiles, day);
            var weeklyHours = profile?.DefaultWeeklyHours ?? _capacityDefaults.BaselineWeeklyHours;
            var profileDailyHours = CapacityPatternCalculator.ResolveProfileDailyHours(profile, weeklyHours, _capacityDefaults);

            var activeOverride = CapacityPatternCalculator.ResolveActiveOverride(overrides, day);
            var dailyHours = CapacityPatternCalculator.ResolveOverrideDailyHours(activeOverride, weeklyHours, profileDailyHours, _capacityDefaults);

            result[day] = dailyHours[day.DayOfWeek];
        }

        return result;
    }

    private async Task<List<EmployeeCapacityProfile>> LoadProfilesAsync(
        int employeeId, DateOnly from, DateOnly to, CancellationToken cancellationToken) =>
        await _dbContext.EmployeeCapacityProfiles
            .AsNoTracking()
            .Where(p => p.EmployeeId == employeeId && p.IsActive &&
                        p.EffectiveFrom <= to && (p.EffectiveTo == null || p.EffectiveTo >= from))
            .ToListAsync(cancellationToken);
}
