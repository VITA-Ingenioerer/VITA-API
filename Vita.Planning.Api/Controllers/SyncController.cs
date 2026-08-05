using Microsoft.AspNetCore.Mvc;
using Vita.Planning.Application.Interfaces;
using Vita.Planning.Infrastructure.Services;
using Vita.Planning.Application.DTOs;

namespace Vita.Planning.Api.Controllers;

[ApiController]
[Route("api/sync")]
public sealed class SyncController : ControllerBase
{
    private static readonly SemaphoreSlim SyncLock = new(1, 1);


    private readonly ISyncRunService _syncRunService;
    private readonly IUserSyncService _userSyncService;
    private readonly IProjectSyncService _projectSyncService;
    private readonly IProjectGroupSyncService _projectGroupSyncService;
    private readonly IProjectCustomerSyncService _projectCustomerSyncService;
    private readonly IProjectStatusSyncService _projectStatusSyncService;
    private readonly IProjectEmployeeGroupSyncService _projectEmployeeGroupSyncService;
    private readonly IProjectEmployeeSyncService _projectEmployeeSyncService;
    private readonly IActivitySyncService _activitySyncService;
    private readonly IProjectActivitySyncService _projectActivitySyncService;
    private readonly ITimeEntrySyncService _timeEntrySyncService;

    public SyncController(
        ISyncRunService syncRunService,
        IUserSyncService userSyncService,
        IProjectSyncService projectSyncService,
        IProjectGroupSyncService projectGroupSyncService,
        IProjectCustomerSyncService projectCustomerSyncService,
        IProjectStatusSyncService projectStatusSyncService,
        IProjectEmployeeGroupSyncService projectEmployeeGroupSyncService,
        IProjectEmployeeSyncService projectEmployeeSyncService,
        IActivitySyncService activitySyncService,
        IProjectActivitySyncService projectActivitySyncService,
        ITimeEntrySyncService timeEntrySyncService)
    {
        _syncRunService = syncRunService;
        _userSyncService = userSyncService;
        _projectSyncService = projectSyncService;
        _projectGroupSyncService = projectGroupSyncService;
        _projectCustomerSyncService = projectCustomerSyncService;
        _projectStatusSyncService = projectStatusSyncService;
        _projectEmployeeGroupSyncService = projectEmployeeGroupSyncService;
        _projectEmployeeSyncService = projectEmployeeSyncService;
        _activitySyncService = activitySyncService;
        _projectActivitySyncService = projectActivitySyncService;
        _timeEntrySyncService = timeEntrySyncService;
    }

    [HttpPost("test-run")]
    public async Task<IActionResult> RunTest(CancellationToken cancellationToken)
    {
        var runId = await _syncRunService.StartRunAsync(
            sourceSystem: "test_source",
            resourceName: "test_resource",
            initiatedBy: "manual-test",
            notes: "Initial sync logging test",
            cancellationToken: cancellationToken);

        await _syncRunService.LogErrorAsync(
            syncRunId: runId,
            sourceSystem: "test_source",
            resourceName: "test_resource",
            errorStage: "test",
            errorMessage: "This is a test error entry",
            recordKey: "TEST-001",
            cancellationToken: cancellationToken);

        await _syncRunService.CompleteRunAsync(
            syncRunId: runId,
            status: "partial",
            rowsRead: 10,
            rowsInserted: 8,
            rowsUpdated: 1,
            errorCount: 1,
            notes: "Test run completed",
            cancellationToken: cancellationToken);

        return Ok(new
        {
            message = "Test sync run completed",
            syncRunId = runId
        });
    }

    [HttpPost("users")]
    public async Task<IActionResult> SyncUsers(CancellationToken cancellationToken)
    {
        var result = await _userSyncService.SyncUsersAsync(
            initiatedBy: "manual-api",
            cancellationToken: cancellationToken);

        return Ok(result);
    }

    [HttpPost("projects")]
    public async Task<IActionResult> SyncProjects(CancellationToken cancellationToken)
    {
        var result = await _projectSyncService.SyncProjectsAsync("manual-api", cancellationToken);
        return Ok(result);
    }

    [HttpPost("project-groups")]
    public async Task<IActionResult> SyncProjectGroups(CancellationToken cancellationToken)
    {
        var result = await _projectGroupSyncService.SyncProjectGroupsAsync(
            initiatedBy: "manual-api",
            cancellationToken: cancellationToken);

        return Ok(result);
    }

    [HttpPost("project-customers")]
    public async Task<IActionResult> SyncProjectCustomers(CancellationToken cancellationToken)
    {
        var result = await _projectCustomerSyncService.SyncProjectCustomersAsync(
            initiatedBy: "manual-api",
            cancellationToken: cancellationToken);

        return Ok(result);
    }

    [HttpPost("project-statuses")]
    public async Task<IActionResult> SyncProjectStatuses(CancellationToken cancellationToken)
    {
        var result = await _projectStatusSyncService.SyncProjectStatusesAsync("manual-api", cancellationToken);
        return Ok(result);
    }

    [HttpPost("project-employee-groups")]
    public async Task<IActionResult> SyncProjectEmployeeGroups(CancellationToken cancellationToken)
    {
        var result = await _projectEmployeeGroupSyncService.SyncProjectEmployeeGroupsAsync("manual-api", cancellationToken);
        return Ok(result);
    }

    [HttpPost("project-employees")]
    public async Task<IActionResult> SyncProjectEmployees(CancellationToken cancellationToken)
    {
        var result = await _projectEmployeeSyncService.SyncProjectEmployeesAsync("manual-api", cancellationToken);
        return Ok(result);
    }

    [HttpPost("activities")]
    public async Task<IActionResult> SyncActivities(CancellationToken cancellationToken)
    {
        var result = await _activitySyncService.SyncActivitiesAsync("manual-api", cancellationToken);
        return Ok(result);
    }

    [HttpPost("project-activities")]
    public async Task<IActionResult> SyncProjectActivities(CancellationToken cancellationToken)
    {
        var result = await _projectActivitySyncService.SyncProjectActivitiesAsync("manual-api", cancellationToken);
        return Ok(result);
    }

    [HttpPost("time-entries/all")]
    public async Task<IActionResult> SyncAllTimeEntries(CancellationToken cancellationToken)
    {
        var result = await _timeEntrySyncService.SyncAllTimeEntriesAsync("manual-api", cancellationToken);
        return Ok(result);
    }

    [HttpPost("time-entries/new")]
    public async Task<IActionResult> SyncNewTimeEntries(CancellationToken cancellationToken)
    {
        var result = await _timeEntrySyncService.SyncNewTimeEntriesAsync("manual-api", cancellationToken);
        return Ok(result);
    }

    [HttpPost("run-all")]
    public async Task<IActionResult> RunAll(CancellationToken cancellationToken)
    {
        if (!await SyncLock.WaitAsync(0, cancellationToken))
        {
            return Conflict(new
            {
                message = "A sync run is already in progress."
            });
        }

        const string initiatedBy = "scheduled-api";

        var run = new SyncRunAllResultDto
        {
            InitiatedBy = initiatedBy,
            StartedAtUtc = DateTime.UtcNow
        };

        async Task RunStepAsync<T>(
            string name,
            Func<CancellationToken, Task<T>> action)
        {
            var step = new SyncRunAllStepResultDto
            {
                Name = name,
                StartedAtUtc = DateTime.UtcNow
            };

            try
            {
                var result = await action(cancellationToken);

                step.Success = true;
                step.Result = result;
                step.CompletedAtUtc = DateTime.UtcNow;

                run.Steps.Add(step);
            }
            catch (Exception ex)
            {
                step.Success = false;
                step.ErrorMessage = ex.Message;
                step.CompletedAtUtc = DateTime.UtcNow;

                run.Steps.Add(step);

                run.Success = false;
                run.ErrorMessage = $"Sync step '{name}' failed: {ex.Message}";

                throw;
            }
        }

        try
        {
            await RunStepAsync(
                "project-statuses",
                ct => _projectStatusSyncService.SyncProjectStatusesAsync(initiatedBy, ct));

            await RunStepAsync(
                "project-groups",
                ct => _projectGroupSyncService.SyncProjectGroupsAsync(initiatedBy, ct));

            await RunStepAsync(
                "project-customers",
                ct => _projectCustomerSyncService.SyncProjectCustomersAsync(initiatedBy, ct));

            await RunStepAsync(
                "project-employee-groups",
                ct => _projectEmployeeGroupSyncService.SyncProjectEmployeeGroupsAsync(initiatedBy, ct));

            await RunStepAsync(
                "project-employees",
                ct => _projectEmployeeSyncService.SyncProjectEmployeesAsync(initiatedBy, ct));

            await RunStepAsync(
                "users",
                ct => _userSyncService.SyncUsersAsync(
                    initiatedBy: initiatedBy,
                    cancellationToken: ct));

            await RunStepAsync(
                "activities",
                ct => _activitySyncService.SyncActivitiesAsync(initiatedBy, ct));

            await RunStepAsync(
                "projects",
                ct => _projectSyncService.SyncProjectsAsync(initiatedBy, ct));

            await RunStepAsync(
                "project-activities",
                ct => _projectActivitySyncService.SyncProjectActivitiesAsync(initiatedBy, ct));

            run.Success = true;
            run.CompletedAtUtc = DateTime.UtcNow;

            return Ok(run);
        }
        catch
        {
            run.CompletedAtUtc = DateTime.UtcNow;

            return StatusCode(
                StatusCodes.Status500InternalServerError,
                run);
        }
        finally
        {
            SyncLock.Release();
        }
    }
}