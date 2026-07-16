using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using Vita.Planning.Application.DTOs;
using Vita.Planning.Application.Interfaces;
using Vita.Planning.Infrastructure.Data;
using Vita.Planning.Infrastructure.Data.Entities;

namespace Vita.Planning.Infrastructure.Services;

public sealed class ProjectLifecycleLogService : IProjectLifecycleLogService
{
    private readonly PlanningDbContext _dbContext;
    private readonly ICorrelationContext _correlationContext;

    public ProjectLifecycleLogService(PlanningDbContext dbContext, ICorrelationContext correlationContext)
    {
        _dbContext = dbContext;
        _correlationContext = correlationContext;
    }

    public async Task<PagedResultDto<ProjectLifecycleLogDto>> GetAllAsync(
        string? targetType = null,
        int? projectNumber = null,
        int? offerId = null,
        string? eventType = null,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize < 1 ? 50 : Math.Min(pageSize, 200);

        var query = _dbContext.ProjectLifecycleLogs.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(targetType))
        {
            query = query.Where(x => x.TargetType == targetType.Trim());
        }

        if (projectNumber.HasValue)
        {
            query = query.Where(x => x.ProjectNumber == projectNumber.Value);
        }

        if (offerId.HasValue)
        {
            query = query.Where(x => x.OfferId == offerId.Value);
        }

        if (!string.IsNullOrWhiteSpace(eventType))
        {
            // Substring match — event types are namespaced like "WorkspaceStep:CreateGroup",
            // and callers frequently want "all WorkspaceStep:* rows" without knowing every suffix.
            var normalizedEventType = eventType.Trim();
            query = query.Where(x => x.EventType.Contains(normalizedEventType));
        }

        query = query.OrderByDescending(x => x.CreatedAtUtc).ThenByDescending(x => x.ProjectLifecycleLogId);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(MapToDtoExpression())
            .ToListAsync(cancellationToken);

        return new PagedResultDto<ProjectLifecycleLogDto>
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize),
            Items = items
        };
    }

    public async Task<IReadOnlyList<ProjectLifecycleLogDto>> GetByCorrelationIdAsync(
        Guid correlationId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.ProjectLifecycleLogs
            .AsNoTracking()
            .Where(x => x.CorrelationId == correlationId)
            .OrderBy(x => x.CreatedAtUtc)
            .Select(MapToDtoExpression())
            .ToListAsync(cancellationToken);
    }

    public async Task<ProjectLifecycleLogDto?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.ProjectLifecycleLogs
            .AsNoTracking()
            .Where(x => x.ProjectLifecycleLogId == id)
            .Select(MapToDtoExpression())
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<ProjectLifecycleLogDto> CreateAsync(
        CreateProjectLifecycleLogRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.TargetType))
        {
            throw new InvalidOperationException("TargetType is required.");
        }

        if (request.TargetType is not ("Project" or "Offer"))
        {
            throw new InvalidOperationException("TargetType must be 'Project' or 'Offer' (constraint: CK_core_project_lifecycle_log_target_type).");
        }

        if (request.TargetType == "Project" && request.ProjectNumber is null)
        {
            throw new InvalidOperationException("ProjectNumber is required when TargetType is 'Project'.");
        }

        if (request.TargetType == "Offer" && request.OfferId is null)
        {
            throw new InvalidOperationException("OfferId is required when TargetType is 'Offer'.");
        }

        if (string.IsNullOrWhiteSpace(request.EventType))
        {
            throw new InvalidOperationException("EventType is required.");
        }

        var entity = new ProjectLifecycleLog
        {
            TargetType = request.TargetType.Trim(),
            ProjectNumber = request.ProjectNumber,
            OfferId = request.OfferId,
            EventType = request.EventType.Trim(),
            EventTitle = NormalizeNullable(request.EventTitle),
            EventDescription = NormalizeNullable(request.EventDescription),
            OldValue = NormalizeNullable(request.OldValue),
            NewValue = NormalizeNullable(request.NewValue),
            SnapshotJson = NormalizeNullable(request.SnapshotJson),
            CreatedBy = NormalizeNullable(request.CreatedBy),
            CreatedAtUtc = DateTime.UtcNow,
            CorrelationId = _correlationContext.CorrelationId
        };

        _dbContext.ProjectLifecycleLogs.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapToDto(entity);
    }

    private static string? NormalizeNullable(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static ProjectLifecycleLogDto MapToDto(ProjectLifecycleLog entity) => new()
    {
        ProjectLifecycleLogId = entity.ProjectLifecycleLogId,
        TargetType = entity.TargetType,
        ProjectNumber = entity.ProjectNumber,
        OfferId = entity.OfferId,
        EventType = entity.EventType,
        EventTitle = entity.EventTitle,
        EventDescription = entity.EventDescription,
        OldValue = entity.OldValue,
        NewValue = entity.NewValue,
        SnapshotJson = entity.SnapshotJson,
        CreatedBy = entity.CreatedBy,
        CreatedAtUtc = entity.CreatedAtUtc,
        CorrelationId = entity.CorrelationId
    };

    private static Expression<Func<ProjectLifecycleLog, ProjectLifecycleLogDto>> MapToDtoExpression() =>
        entity => new ProjectLifecycleLogDto
        {
            ProjectLifecycleLogId = entity.ProjectLifecycleLogId,
            TargetType = entity.TargetType,
            ProjectNumber = entity.ProjectNumber,
            OfferId = entity.OfferId,
            EventType = entity.EventType,
            EventTitle = entity.EventTitle,
            EventDescription = entity.EventDescription,
            OldValue = entity.OldValue,
            NewValue = entity.NewValue,
            SnapshotJson = entity.SnapshotJson,
            CreatedBy = entity.CreatedBy,
            CreatedAtUtc = entity.CreatedAtUtc,
            CorrelationId = entity.CorrelationId
        };
}
