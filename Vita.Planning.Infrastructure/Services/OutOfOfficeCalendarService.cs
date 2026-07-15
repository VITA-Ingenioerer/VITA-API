using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Vita.Planning.Application.DTOs;
using Vita.Planning.Application.Interfaces;
using Vita.Planning.Infrastructure.Data;

namespace Vita.Planning.Infrastructure.Services;

public sealed class OutOfOfficeCalendarService : IOutOfOfficeCalendarService
{
    private readonly PlanningDbContext _dbContext;
    private readonly IOutlookCalendarClient _calendarClient;
    private readonly IBusinessEventService _events;
    private readonly ILogger<OutOfOfficeCalendarService> _logger;

    public OutOfOfficeCalendarService(
        PlanningDbContext dbContext,
        IOutlookCalendarClient calendarClient,
        IBusinessEventService events,
        ILogger<OutOfOfficeCalendarService> logger)
    {
        _dbContext = dbContext;
        _calendarClient = calendarClient;
        _events = events;
        _logger = logger;
    }

    public async Task<OutOfOfficeCalendarEventDto> CreateAsync(
        int employeeId,
        CreateOutOfOfficeCalendarEventRequest request,
        CallerInfo caller,
        CancellationToken cancellationToken = default)
    {
        if (request.EndDate < request.StartDate)
        {
            throw new InvalidOperationException("EndDate must be on or after StartDate.");
        }

        var userPrincipalName = await _dbContext.Users
            .AsNoTracking()
            .Where(u => u.EmployeeId == employeeId)
            .Select(u => u.UserPrincipalName)
            .FirstOrDefaultAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(userPrincipalName))
        {
            throw new KeyNotFoundException($"User with employee id '{employeeId}' was not found.");
        }

        var graphEventId = await _calendarClient.CreateOutOfOfficeEventAsync(
            userPrincipalName, request.Title, request.StartDate, request.EndDate, cancellationToken);

        // The Graph event above is the actual operation the caller asked for and it has
        // already succeeded at this point. The audit trail below is a secondary,
        // non-essential side effect — it must never turn a real success into a reported
        // failure, so its own failures are logged and swallowed rather than rethrown.
        try
        {
            await _events.RecordAsync(new RecordBusinessEventRequest
            {
                EventType = "OutOfOfficeCalendarEventCreated",
                EventTitle = $"Out of office oprettet: {request.Title} ({request.StartDate:yyyy-MM-dd} - {request.EndDate:yyyy-MM-dd})",
                EntityType = "CalendarEvent",
                EntityId = graphEventId,
                NewValue = $"{request.Title}: {request.StartDate:yyyy-MM-dd} - {request.EndDate:yyyy-MM-dd}",
                CreatedByUserId = caller.UserId,
                CreatedByName = caller.Name,
                SourceModule = "OutOfOfficeCalendarService"
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to record the audit event for out-of-office calendar event {GraphEventId} (employee {EmployeeId}). The Graph event was already created successfully.",
                graphEventId,
                employeeId);
        }

        return new OutOfOfficeCalendarEventDto
        {
            GraphEventId = graphEventId,
            EmployeeId = employeeId,
            Title = request.Title,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            CreatedAtUtc = DateTime.UtcNow
        };
    }
}
