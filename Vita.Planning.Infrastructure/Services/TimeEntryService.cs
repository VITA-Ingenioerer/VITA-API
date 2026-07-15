using System.Net;
using Vita.Planning.Application.DTOs;
using Vita.Planning.Application.Exceptions;
using Vita.Planning.Application.Interfaces;

namespace Vita.Planning.Infrastructure.Services;

public sealed class TimeEntryService : ITimeEntryService
{
    private readonly IEconomicTimeEntryClient _client;

    public TimeEntryService(IEconomicTimeEntryClient client)
    {
        _client = client;
    }

    public Task<IReadOnlyList<EconomicTimeEntryDto>> GetTimeEntriesAsync(
        int employeeNumber,
        DateTime fromDate,
        DateTime? toDate,
        CancellationToken cancellationToken = default)
    {
        return _client.GetTimeEntriesAsync(employeeNumber, fromDate, toDate, cancellationToken);
    }

    public Task<EconomicTimeEntryDto?> GetTimeEntryAsync(int number, CancellationToken cancellationToken = default)
    {
        return _client.GetTimeEntryAsync(number, cancellationToken);
    }

    public Task<int> CreateTimeEntryAsync(CreateTimeEntryRequest request, CancellationToken cancellationToken = default)
    {
        return _client.CreateTimeEntryAsync(request, cancellationToken);
    }

    public async Task UpdateTimeEntryAsync(UpdateTimeEntryRequest request, CancellationToken cancellationToken = default)
    {
        var existingEntry = await _client.GetTimeEntryAsync(request.Number, cancellationToken);

        if (existingEntry is null)
        {
            throw new KeyNotFoundException($"Time entry '{request.Number}' was not found.");
        }

        if (existingEntry.IsApproved == true || existingEntry.IsReconciled == true)
        {
            throw new InvalidOperationException(
                $"Time entry '{request.Number}' cannot be updated because it is approved or reconciled.");
        }

        var updateRequest = new UpdateTimeEntryRequest
        {
            Number = request.Number,
            ProjectNumber = request.ProjectNumber,
            ActivityNumber = request.ActivityNumber,
            EmployeeNumber = request.EmployeeNumber,
            Date = request.Date,
            NumberOfHours = request.NumberOfHours,
            Text = request.Text,
            ObjectVersion = existingEntry.ObjectVersion
        };

        await _client.UpdateTimeEntryAsync(updateRequest, cancellationToken);
    }

    public async Task ApproveTimeEntryAsync(int number, CancellationToken cancellationToken = default)
    {
        var result = await ApproveTimeEntriesAsync([number], cancellationToken: cancellationToken);

        if (result.Approved.Contains(number))
        {
            return;
        }

        var failure = result.Failed.First(f => f.Number == number);

        throw failure.ErrorCode switch
        {
            "NotFound" => new KeyNotFoundException(failure.Message),
            "Reconciled" or "AlreadyApproved" => new InvalidOperationException(failure.Message),
            _ => new EconomicApprovalException(failure.Title, failure.Message, failure.ErrorCode ?? "Unknown")
        };
    }

    public async Task<ApproveTimeEntriesResult> ApproveTimeEntriesAsync(
        IReadOnlyList<int> numbers,
        int? bookOn = null,
        CancellationToken cancellationToken = default)
    {
        if (numbers.Count == 0)
        {
            throw new InvalidOperationException("At least one time entry number is required.");
        }

        var uniqueNumbers = numbers.Distinct().ToList();
        var approved = new List<int>();
        var failed = new List<TimeEntryApprovalFailure>();
        var candidates = new List<int>();
        var entriesByNumber = new Dictionary<int, EconomicTimeEntryDto>();

        foreach (var number in uniqueNumbers)
        {
            var existingEntry = await _client.GetTimeEntryAsync(number, cancellationToken);

            if (existingEntry is null)
            {
                failed.Add(new TimeEntryApprovalFailure
                {
                    Number = number,
                    Title = "Tidsregistrering ikke fundet",
                    Message = $"Time entry '{number}' was not found.",
                    ErrorCode = "NotFound"
                });
                continue;
            }

            entriesByNumber[number] = existingEntry;

            if (existingEntry.IsReconciled == true)
            {
                failed.Add(BuildFailure(existingEntry, "Kan ikke godkende tidsregistrering",
                    $"Time entry '{number}' cannot be approved because it is reconciled.", "Reconciled"));
                continue;
            }

            if (existingEntry.IsApproved == true)
            {
                failed.Add(BuildFailure(existingEntry, "Allerede godkendt",
                    $"Time entry '{number}' is already approved.", "AlreadyApproved"));
                continue;
            }

            candidates.Add(number);
        }

        if (candidates.Count == 0)
        {
            return new ApproveTimeEntriesResult { Approved = approved, Failed = failed };
        }

        try
        {
            await _client.ApproveTimeEntriesAsync(candidates, bookOn, cancellationToken);
            approved.AddRange(candidates);
        }
        catch (EconomicApprovalException ex) when (candidates.Count > 1 && ex.StatusCode == HttpStatusCode.Conflict)
        {
            // e-conomic rejects the whole batch atomically on 409 (e.g. one entry sits in a
            // barred accounting period), so retry one by one to let the rest through.
            foreach (var number in candidates)
            {
                try
                {
                    await _client.ApproveTimeEntriesAsync([number], bookOn, cancellationToken);
                    approved.Add(number);
                }
                catch (EconomicApprovalException itemEx)
                {
                    failed.Add(BuildFailure(entriesByNumber[number], itemEx.Title, itemEx.Message, itemEx.ErrorCode));
                }
            }
        }
        catch (EconomicApprovalException ex)
        {
            // Non-409 failures (auth, rate limiting, server errors) apply to the whole
            // batch identically, so report every candidate as failed without retrying.
            foreach (var number in candidates)
            {
                failed.Add(BuildFailure(entriesByNumber[number], ex.Title, ex.Message, ex.ErrorCode));
            }
        }

        return new ApproveTimeEntriesResult { Approved = approved, Failed = failed };
    }

    private static TimeEntryApprovalFailure BuildFailure(
        EconomicTimeEntryDto entry, string title, string message, string? errorCode)
    {
        return new TimeEntryApprovalFailure
        {
            Number = entry.Number,
            Date = entry.Date,
            Hours = entry.NumberOfHours,
            Title = title,
            Message = message,
            ErrorCode = errorCode
        };
    }

    public Task DeleteTimeEntryAsync(int number, CancellationToken cancellationToken = default)
    {
        return _client.DeleteTimeEntryAsync(number, cancellationToken);
    }
}
