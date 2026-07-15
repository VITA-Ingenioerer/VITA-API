using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Vita.Planning.Application.DTOs;
using Vita.Planning.Application.Interfaces;
using Vita.Planning.Infrastructure.Data;

namespace Vita.Planning.Infrastructure.Services;

public sealed class AbsenceRegistrationService : IAbsenceRegistrationService
{
    private readonly IEconomicTimeEntryClient _timeEntryClient;
    private readonly PlanningDbContext _dbContext;
    private readonly AbsenceSettings _settings;

    public AbsenceRegistrationService(
        IEconomicTimeEntryClient timeEntryClient,
        PlanningDbContext dbContext,
        IOptions<AbsenceSettings> settings)
    {
        _timeEntryClient = timeEntryClient;
        _dbContext = dbContext;
        _settings = settings.Value;
    }

    public async Task<IReadOnlyList<AbsenceRegistrationDto>> GetAbsenceRegistrationsAsync(
        DateOnly fromDate,
        DateOnly toDate,
        int? employeeId = null,
        CancellationToken cancellationToken = default)
    {
        var entries = await _timeEntryClient.GetTimeEntriesByProjectAsync(
            _settings.ProjectNumber,
            fromDate.ToDateTime(TimeOnly.MinValue),
            toDate.ToDateTime(TimeOnly.MinValue),
            employeeId,
            cancellationToken);

        if (entries.Count == 0)
        {
            return [];
        }

        var employeeIds = entries.Select(x => x.EmployeeNumber).Distinct().ToList();
        var displayNamesByEmployeeId = await _dbContext.Users
            .AsNoTracking()
            .Where(u => employeeIds.Contains(u.EmployeeId))
            .ToDictionaryAsync(u => u.EmployeeId, u => u.DisplayName, cancellationToken);

        return entries
            .OrderBy(x => x.Date)
            .ThenBy(x => x.EmployeeNumber)
            .Select(x => new AbsenceRegistrationDto
            {
                TimeEntryNumber = x.Number,
                EmployeeId = x.EmployeeNumber,
                EmployeeDisplayName = displayNamesByEmployeeId.GetValueOrDefault(x.EmployeeNumber),
                ProjectNumber = x.ProjectNumber,
                ActivityNumber = x.ActivityNumber,
                Date = DateOnly.FromDateTime(x.Date),
                Hours = x.NumberOfHours ?? 0,
                Text = x.Text,
                IsApproved = x.IsApproved
            })
            .ToList();
    }
}
