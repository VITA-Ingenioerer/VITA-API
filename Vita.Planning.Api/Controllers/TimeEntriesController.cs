using Microsoft.AspNetCore.Mvc;
using Vita.Planning.Application.DTOs;
using Vita.Planning.Application.Exceptions;
using Vita.Planning.Application.Interfaces;

namespace Vita.Planning.Api.Controllers;

[ApiController]
[Route("api/time-entries")]
public sealed class TimeEntriesController : ControllerBase
{
    private readonly ITimeEntryService _service;
    private readonly IBusinessEventService _events;

    public TimeEntriesController(ITimeEntryService service, IBusinessEventService events)
    {
        _service = service;
        _events = events;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<EconomicTimeEntryDto>>> GetTimeEntries(
        [FromQuery] int employeeNumber,
        [FromQuery] DateTime fromDate,
        [FromQuery] DateTime? toDate,
        CancellationToken cancellationToken)
    {
        var entries = await _service.GetTimeEntriesAsync(employeeNumber, fromDate, toDate, cancellationToken);
        return Ok(entries);
    }

    [HttpGet("{number:int}")]
    public async Task<ActionResult<EconomicTimeEntryDto>> GetTimeEntry(int number, CancellationToken cancellationToken)
    {
        var entry = await _service.GetTimeEntryAsync(number, cancellationToken);

        if (entry is null)
        {
            return NotFound();
        }

        return Ok(entry);
    }

    [HttpPost]
    public async Task<ActionResult<int>> CreateTimeEntry(
        [FromBody] CreateTimeEntryRequest request,
        CancellationToken cancellationToken)
    {
        var caller = CallerInfo.FromClaimsPrincipal(User);

        try
        {
            var number = await _service.CreateTimeEntryAsync(request, cancellationToken);

            await RecordEventAsync(
                "TimeEntryCreated", $"Tidsregistrering oprettet: {number}", number.ToString(),
                $"Projekt {request.ProjectNumber}, {request.NumberOfHours}t, {request.Date:yyyy-MM-dd}",
                caller, cancellationToken);

            return CreatedAtAction(nameof(GetTimeEntry), new { number }, number);
        }
        catch (Exception ex)
        {
            await RecordEventAsync(
                "TimeEntryCreateFailed", "Oprettelse af tidsregistrering fejlede", "unknown",
                ex.Message, caller, cancellationToken);
            throw;
        }
    }

    [HttpPut]
    public async Task<IActionResult> UpdateTimeEntry(
        [FromBody] UpdateTimeEntryRequest request,
        CancellationToken cancellationToken)
    {
        var caller = CallerInfo.FromClaimsPrincipal(User);

        try
        {
            await _service.UpdateTimeEntryAsync(request, cancellationToken);

            await RecordEventAsync(
                "TimeEntryUpdated", $"Tidsregistrering opdateret: {request.Number}", request.Number.ToString(),
                $"{request.NumberOfHours}t, {request.Date:yyyy-MM-dd}", caller, cancellationToken);

            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            await RecordEventAsync(
                "TimeEntryUpdateFailed", $"Opdatering fejlede: {request.Number} ikke fundet", request.Number.ToString(),
                ex.Message, caller, cancellationToken);
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            await RecordEventAsync(
                "TimeEntryUpdateFailed", $"Opdatering fejlede: {request.Number}", request.Number.ToString(),
                ex.Message, caller, cancellationToken);
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpPost("approve")]
    public async Task<ActionResult<ApproveTimeEntriesResult>> ApproveTimeEntries(
        [FromBody] ApproveTimeEntriesRequest request,
        CancellationToken cancellationToken)
    {
        var caller = CallerInfo.FromClaimsPrincipal(User);
        var result = await _service.ApproveTimeEntriesAsync(request.Numbers, request.BookOn, cancellationToken);

        await RecordEventAsync(
            "TimeEntriesApproved", $"Tidsregistreringer godkendt: {result.Approved.Count}/{request.Numbers.Count}",
            "bulk", $"{result.Approved.Count} godkendt, {result.Failed.Count} fejlede", caller, cancellationToken);

        return StatusCode(StatusCodes.Status207MultiStatus, result);
    }

    [HttpPost("{number:int}/approve")]
    public async Task<IActionResult> ApproveTimeEntry(int number, CancellationToken cancellationToken)
    {
        var caller = CallerInfo.FromClaimsPrincipal(User);

        try
        {
            await _service.ApproveTimeEntryAsync(number, cancellationToken);

            await RecordEventAsync(
                "TimeEntryApproved", $"Tidsregistrering godkendt: {number}", number.ToString(),
                null, caller, cancellationToken);

            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (EconomicApprovalException ex)
        {
            await RecordEventAsync(
                "TimeEntryApproveFailed", $"Godkendelse fejlede: {number}", number.ToString(),
                ex.Message, caller, cancellationToken);
            return Conflict(new { title = ex.Title, message = ex.Message, errorCode = ex.ErrorCode });
        }
        catch (InvalidOperationException ex)
        {
            await RecordEventAsync(
                "TimeEntryApproveFailed", $"Godkendelse fejlede: {number}", number.ToString(),
                ex.Message, caller, cancellationToken);
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpDelete("{number:int}")]
    public async Task<IActionResult> DeleteTimeEntry(int number, CancellationToken cancellationToken)
    {
        var caller = CallerInfo.FromClaimsPrincipal(User);

        await _service.DeleteTimeEntryAsync(number, cancellationToken);

        await RecordEventAsync(
            "TimeEntryDeleted", $"Tidsregistrering slettet: {number}", number.ToString(),
            null, caller, cancellationToken);

        return NoContent();
    }

    private Task RecordEventAsync(
        string eventType,
        string eventTitle,
        string entityId,
        string? newValue,
        CallerInfo caller,
        CancellationToken cancellationToken) =>
        _events.RecordAsync(new RecordBusinessEventRequest
        {
            EventType = eventType,
            EventTitle = eventTitle,
            EntityType = "TimeEntry",
            EntityId = entityId,
            NewValue = newValue,
            CreatedByUserId = caller.UserId,
            CreatedByName = caller.Name,
            SourceModule = "TimeEntriesController"
        }, cancellationToken);
}
