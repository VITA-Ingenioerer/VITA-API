using Vita.Planning.Application.DTOs;

namespace Vita.Planning.Application.Interfaces;

public interface IResourcePlanSnapshotService
{
    Task<ResourcePlanSnapshotDto> CreateAsync(
        CreateResourcePlanSnapshotRequest request,
        CallerInfo caller,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ResourcePlanSnapshotDto>> GetAllAsync(
        int? scenarioId = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ResourcePlanSnapshotEntryDto>> GetEntriesAsync(
        long snapshotId,
        int? employeeId = null,
        int? planningTargetId = null,
        CancellationToken cancellationToken = default);

    Task<ResourcePlanSnapshotComparisonDto> CompareAsync(
        long fromSnapshotId,
        long toSnapshotId,
        int? employeeId = null,
        int? planningTargetId = null,
        CancellationToken cancellationToken = default);
}
