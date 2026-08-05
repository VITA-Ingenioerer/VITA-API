using Vita.Planning.Application.DTOs;

namespace Vita.Planning.Application.Interfaces;

public interface ITimeEntrySyncService
{
    /// <summary>
    /// Full backfill: pulls every time entry from e-conomic and upserts it into
    /// ext.time_entries. Meant to be run once (or re-run deliberately), not scheduled.
    /// </summary>
    Task<TimeEntrySyncResultDto> SyncAllTimeEntriesAsync(
        string initiatedBy, CancellationToken cancellationToken = default);

    /// <summary>
    /// Incremental sync: pulls only entries changed since the newest last_updated
    /// already stored locally. Falls back to a full sync if the table is empty.
    /// Safe to run on a schedule.
    /// </summary>
    Task<TimeEntrySyncResultDto> SyncNewTimeEntriesAsync(
        string initiatedBy, CancellationToken cancellationToken = default);
}
