using Microsoft.EntityFrameworkCore;
using Vita.Planning.Application.Interfaces;
using Vita.Planning.Infrastructure.Data;

namespace Vita.Planning.Api.HostedServices;

public sealed class SharePointWorkspacePollerService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SharePointWorkspacePollerService> _logger;

    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan MaxAge = TimeSpan.FromDays(7);

    public SharePointWorkspacePollerService(
        IServiceScopeFactory scopeFactory,
        ILogger<SharePointWorkspacePollerService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("SharePoint workspace poller started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(PollInterval, stoppingToken);

            try
            {
                await PollPendingWorkspacesAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SharePoint workspace poller encountered an error.");
            }
        }
    }

    private async Task PollPendingWorkspacesAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlanningDbContext>();
        var provisioningClient = scope.ServiceProvider.GetRequiredService<IProjectWorkspaceProvisioningClient>();

        var cutoff = DateTime.UtcNow.Subtract(MaxAge);

        // Find projects that have a group ID (provisioning started) but no site URL yet
        var pending = await db.ProjectMetadata
            .Where(p =>
                p.ProjectArchiveGroupId != null &&
                p.ProjectArchiveUrl == null &&
                p.CreatedAtUtc >= cutoff)
            .ToListAsync(cancellationToken);

        if (pending.Count == 0)
            return;

        _logger.LogInformation(
            "SharePoint workspace poller: checking {Count} pending project(s).", pending.Count);

        foreach (var project in pending)
        {
            try
            {
                var (siteId, driveId, siteWebUrl) = await provisioningClient.TryGetGroupSiteAsync(
                    project.ProjectArchiveGroupId!,
                    cancellationToken);

                if (siteWebUrl is null)
                    continue;

                project.ProjectArchiveUrl = siteWebUrl;
                project.ProjectArchiveSiteId ??= siteId;
                project.ProjectArchiveDriveId ??= driveId;

                // Mirror back to the original offer
                if (project.OriginalOfferId.HasValue)
                {
                    var offer = await db.Offers.FindAsync(
                        [project.OriginalOfferId.Value], cancellationToken);

                    if (offer is not null)
                    {
                        offer.ProjectArchiveUrl = siteWebUrl;
                        offer.ProjectArchiveSiteId ??= siteId;
                        offer.ProjectArchiveDriveId ??= driveId;
                    }
                }

                _logger.LogInformation(
                    "SharePoint workspace poller: resolved site URL for project {ProjectNumber}: {Url}",
                    project.ProjectNumber, siteWebUrl);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "SharePoint workspace poller: failed to resolve site URL for project {ProjectNumber}.",
                    project.ProjectNumber);
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
