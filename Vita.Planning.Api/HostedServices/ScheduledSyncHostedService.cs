using Vita.Planning.Application.Interfaces;

namespace Vita.Planning.Api.HostedServices;

public sealed class ScheduledSyncHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ScheduledSyncHostedService> _logger;
    private readonly IConfiguration _configuration;

    private static readonly SemaphoreSlim SyncLock = new(1, 1);

    private static readonly TimeZoneInfo DanishTimeZone =
        TimeZoneInfo.FindSystemTimeZoneById("Romance Standard Time");

    public ScheduledSyncHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<ScheduledSyncHostedService> logger,
        IConfiguration configuration)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Scheduled sync hosted service started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var isEnabled = _configuration.GetValue<bool>("SyncScheduler:Enabled");

                if (!isEnabled)
                {
                    await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
                    continue;
                }

                var nowUtc = DateTimeOffset.UtcNow;
                var nowDanish = TimeZoneInfo.ConvertTime(nowUtc, DanishTimeZone);

                if (ShouldRun(nowDanish))
                {
                    await RunSyncAsync(stoppingToken);
                    await DelayUntilNextHourAsync(stoppingToken);
                }
                else
                {
                    await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                // Normal shutdown/restart
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Scheduled sync hosted service failed unexpectedly.");
                await Task.Delay(TimeSpan.FromMinutes(10), stoppingToken);
            }
        }

        _logger.LogInformation("Scheduled sync hosted service stopped.");
    }

    private static bool ShouldRun(DateTimeOffset nowDanish)
    {
        var isWeekday =
            nowDanish.DayOfWeek is not DayOfWeek.Saturday
            and not DayOfWeek.Sunday;

        var isWorkHour =
            nowDanish.Hour is >= 7 and <= 18;

        var isNearStartOfTenMinutes =
            nowDanish.Minute % 10 < 5;

        return isWeekday && isWorkHour && isNearStartOfTenMinutes;
    }

    private async Task RunSyncAsync(CancellationToken cancellationToken)
    {
        if (!await SyncLock.WaitAsync(0, cancellationToken))
        {
            _logger.LogWarning("Scheduled sync skipped because another sync is already running.");
            return;
        }

        try
        {
            const string initiatedBy = "scheduled-hosted-service";

            _logger.LogInformation("Starting scheduled sync run.");

            using var scope = _scopeFactory.CreateScope();

            var projectStatusSyncService =
                scope.ServiceProvider.GetRequiredService<IProjectStatusSyncService>();

            var projectGroupSyncService =
                scope.ServiceProvider.GetRequiredService<IProjectGroupSyncService>();

            var projectCustomerSyncService =
                scope.ServiceProvider.GetRequiredService<IProjectCustomerSyncService>();

            var projectEmployeeGroupSyncService =
                scope.ServiceProvider.GetRequiredService<IProjectEmployeeGroupSyncService>();

            var projectEmployeeSyncService =
                scope.ServiceProvider.GetRequiredService<IProjectEmployeeSyncService>();

            var userSyncService =
                scope.ServiceProvider.GetRequiredService<IUserSyncService>();

            var activitySyncService =
                scope.ServiceProvider.GetRequiredService<IActivitySyncService>();

            var projectSyncService =
                scope.ServiceProvider.GetRequiredService<IProjectSyncService>();

            await RunStepAsync(
                "project-statuses",
                () => projectStatusSyncService.SyncProjectStatusesAsync(initiatedBy, cancellationToken));

            await RunStepAsync(
                "project-groups",
                () => projectGroupSyncService.SyncProjectGroupsAsync(initiatedBy, cancellationToken));

            await RunStepAsync(
                "project-customers",
                () => projectCustomerSyncService.SyncProjectCustomersAsync(initiatedBy, cancellationToken));

            await RunStepAsync(
                "project-employee-groups",
                () => projectEmployeeGroupSyncService.SyncProjectEmployeeGroupsAsync(initiatedBy, cancellationToken));

            await RunStepAsync(
                "project-employees",
                () => projectEmployeeSyncService.SyncProjectEmployeesAsync(initiatedBy, cancellationToken));

            await RunStepAsync(
                "users",
                () => userSyncService.SyncUsersAsync(
                    initiatedBy: initiatedBy,
                    cancellationToken: cancellationToken));

            await RunStepAsync(
                "activities",
                () => activitySyncService.SyncActivitiesAsync(initiatedBy, cancellationToken));

            await RunStepAsync(
                "projects",
                () => projectSyncService.SyncProjectsAsync(initiatedBy, cancellationToken));

            _logger.LogInformation("Scheduled sync run completed.");
        }
        finally
        {
            SyncLock.Release();
        }
    }

    private async Task RunStepAsync<T>(
        string stepName,
        Func<Task<T>> action)
    {
        var startedAtUtc = DateTime.UtcNow;

        _logger.LogInformation("Starting scheduled sync step {StepName}.", stepName);

        try
        {
            var result = await action();

            _logger.LogInformation(
                "Completed scheduled sync step {StepName} in {DurationSeconds} seconds. Result: {@Result}",
                stepName,
                (DateTime.UtcNow - startedAtUtc).TotalSeconds,
                result);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Scheduled sync step {StepName} failed after {DurationSeconds} seconds.",
                stepName,
                (DateTime.UtcNow - startedAtUtc).TotalSeconds);

            throw;
        }
    }

    private static async Task DelayUntilNextHourAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;

        var nextHour = new DateTimeOffset(
            now.Year,
            now.Month,
            now.Day,
            now.Hour,
            0,
            0,
            TimeSpan.Zero).AddHours(1);

        var delay = nextHour - now;

        if (delay < TimeSpan.FromMinutes(1))
        {
            delay = TimeSpan.FromMinutes(1);
        }

        await Task.Delay(delay, cancellationToken);
    }
}