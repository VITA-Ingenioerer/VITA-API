using Vita.Planning.Application.DTOs;

namespace Vita.Planning.Application.Interfaces;

public interface ISyncRunService
{
    Task<IReadOnlyList<SyncRunDto>> QueryRunsAsync(
        string? sourceSystem = null,
        string? status = null,
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        int take = 200,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SyncErrorDto>> GetErrorsByRunIdAsync(
        long syncRunId,
        CancellationToken cancellationToken = default);

    Task<long> StartRunAsync(
        string sourceSystem,
        string resourceName,
        string initiatedBy,
        string? notes = null,
        CancellationToken cancellationToken = default);

    Task CompleteRunAsync(
        long syncRunId,
        string status,
        int? rowsRead = null,
        int? rowsInserted = null,
        int? rowsUpdated = null,
        int? rowsDeleted = null,
        int errorCount = 0,
        string? notes = null,
        CancellationToken cancellationToken = default);

    Task LogErrorAsync(
        long syncRunId,
        string sourceSystem,
        string resourceName,
        string errorStage,
        string errorMessage,
        string? recordKey = null,
        string? errorCode = null,
        string? rawPayload = null,
        CancellationToken cancellationToken = default);
}