using Vita.Planning.Application.DTOs;
using Vita.Planning.Application.Interfaces;

namespace Vita.Planning.Infrastructure.Services;

public sealed class CorrelationTraceService : ICorrelationTraceService
{
    private const int MaxEntriesPerSource = 200;

    private readonly IBusinessEventService _businessEventService;
    private readonly IErrorLogService _errorLogService;
    private readonly ISyncRunService _syncRunService;
    private readonly IProjectLifecycleLogService _projectLifecycleLogService;
    private readonly IResourcePlanEntryHistoryService _resourcePlanEntryHistoryService;

    public CorrelationTraceService(
        IBusinessEventService businessEventService,
        IErrorLogService errorLogService,
        ISyncRunService syncRunService,
        IProjectLifecycleLogService projectLifecycleLogService,
        IResourcePlanEntryHistoryService resourcePlanEntryHistoryService)
    {
        _businessEventService = businessEventService;
        _errorLogService = errorLogService;
        _syncRunService = syncRunService;
        _projectLifecycleLogService = projectLifecycleLogService;
        _resourcePlanEntryHistoryService = resourcePlanEntryHistoryService;
    }

    public async Task<CorrelationTraceDto> GetTraceAsync(Guid correlationId, CancellationToken cancellationToken = default)
    {
        var businessEventsTask = _businessEventService.GetByCorrelationIdAsync(correlationId, cancellationToken);
        var errorsTask = _errorLogService.QueryAsync(correlationId, page: 1, pageSize: MaxEntriesPerSource, cancellationToken: cancellationToken);
        var syncRunsTask = _syncRunService.GetByCorrelationIdAsync(correlationId, cancellationToken);
        var lifecycleLogTask = _projectLifecycleLogService.GetByCorrelationIdAsync(correlationId, cancellationToken);
        var resourcePlanEntryHistoryTask = _resourcePlanEntryHistoryService.GetByCorrelationIdAsync(correlationId, cancellationToken);

        await Task.WhenAll(businessEventsTask, errorsTask, syncRunsTask, lifecycleLogTask, resourcePlanEntryHistoryTask);

        var syncRuns = syncRunsTask.Result;
        var syncErrorsByRun = await Task.WhenAll(
            syncRuns.Select(run => _syncRunService.GetErrorsByRunIdAsync(run.SyncRunId, cancellationToken)));

        var entries = new List<TraceEntryDto>();
        entries.AddRange(businessEventsTask.Result.Select(MapBusinessEvent));
        entries.AddRange(errorsTask.Result.Items.Select(MapError));
        entries.AddRange(syncRuns.Select(MapSyncRun));
        entries.AddRange(syncErrorsByRun.SelectMany(x => x).Select(MapSyncError));
        entries.AddRange(lifecycleLogTask.Result.Select(MapLifecycleLog));
        entries.AddRange(resourcePlanEntryHistoryTask.Result.Select(MapResourcePlanEntryHistory));

        return new CorrelationTraceDto
        {
            CorrelationId = correlationId,
            Entries = entries.OrderBy(x => x.TimestampUtc).ToList()
        };
    }

    private static TraceEntryDto MapBusinessEvent(BusinessEventDto x) => new()
    {
        TimestampUtc = x.CreatedAtUtc,
        Source = TraceSource.BusinessEvent,
        Title = x.EventTitle ?? x.EventType,
        Description = x.EventDescription,
        Severity = "info",
        EntityType = x.EntityType,
        EntityId = x.EntityId,
        Actor = x.CreatedByName ?? x.CreatedByUserId,
        SourceId = x.BusinessEventId
    };

    private static TraceEntryDto MapError(OpsErrorDto x) => new()
    {
        TimestampUtc = x.CreatedAtUtc,
        Source = TraceSource.Error,
        Title = x.ExceptionType,
        Description = x.Message,
        Severity = "error",
        EntityType = x.RequestPath,
        EntityId = x.RequestMethod,
        Actor = x.CallerName ?? x.CallerUserId,
        SourceId = x.OpsErrorId
    };

    private static TraceEntryDto MapSyncRun(SyncRunDto x) => new()
    {
        TimestampUtc = x.StartedAtUtc,
        Source = TraceSource.SyncRun,
        Title = $"{x.SourceSystem} sync — {x.ResourceName}",
        Description = x.Notes,
        Severity = x.Status.Equals("failed", StringComparison.OrdinalIgnoreCase)
            || x.Status.Equals("error", StringComparison.OrdinalIgnoreCase)
                ? "error"
                : x.ErrorCount > 0 ? "warning" : "info",
        EntityType = x.SourceSystem,
        EntityId = x.ResourceName,
        Actor = x.InitiatedBy,
        SourceId = x.SyncRunId
    };

    private static TraceEntryDto MapSyncError(SyncErrorDto x) => new()
    {
        TimestampUtc = x.CreatedAtUtc,
        Source = TraceSource.SyncError,
        Title = $"{x.SourceSystem} error — {x.ErrorStage}",
        Description = x.ErrorMessage,
        Severity = "error",
        EntityType = x.ResourceName,
        EntityId = x.RecordKey,
        Actor = null,
        SourceId = x.SyncErrorId
    };

    private static TraceEntryDto MapLifecycleLog(ProjectLifecycleLogDto x) => new()
    {
        TimestampUtc = x.CreatedAtUtc,
        Source = TraceSource.LifecycleLog,
        Title = x.EventTitle ?? x.EventType,
        Description = x.EventDescription,
        Severity = "info",
        EntityType = x.TargetType,
        EntityId = x.ProjectNumber?.ToString() ?? x.OfferId?.ToString(),
        Actor = x.CreatedBy,
        SourceId = x.ProjectLifecycleLogId
    };

    private static TraceEntryDto MapResourcePlanEntryHistory(ResourcePlanEntryHistoryDto x) => new()
    {
        TimestampUtc = x.ChangedAtUtc,
        Source = TraceSource.ResourcePlanEntryHistory,
        Title = $"{x.ChangeType} — {x.OldHours ?? 0}h → {x.NewHours ?? 0}h",
        Description = x.ChangeReason ?? x.NewDescription,
        Severity = "info",
        EntityType = "ResourcePlanEntry",
        EntityId = x.ResourcePlanEntryId?.ToString() ?? x.ResourcePlanId.ToString(),
        Actor = x.ChangedByName ?? x.ChangedByUserId,
        SourceId = x.ResourcePlanEntryHistoryId
    };
}
