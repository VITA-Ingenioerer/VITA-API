using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using Vita.Planning.Application.DTOs;
using Vita.Planning.Application.Interfaces;
using Vita.Planning.Infrastructure.Data;
using Vita.Planning.Infrastructure.Data.Entities;

namespace Vita.Planning.Infrastructure.Services;

public sealed class ResourcePlanService : IResourcePlanService
{
    private readonly PlanningDbContext _dbContext;

    public ResourcePlanService(PlanningDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<ResourcePlanDto>> GetAllAsync(int? scenarioId = null, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.ResourcePlans.AsNoTracking();

        if (scenarioId.HasValue)
        {
            query = query.Where(x => x.ScenarioId == scenarioId.Value);
        }

        return await query
            .OrderBy(x => x.EmployeeId)
            .ThenBy(x => x.ScenarioId)
            .ThenBy(x => x.StartYear)
            .ThenBy(x => x.StartMonth)
            .Select(MapToDtoExpression())
            .ToListAsync(cancellationToken);
    }

    public async Task<ResourcePlanDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.ResourcePlans
            .AsNoTracking()
            .Where(x => x.ResourcePlanId == id)
            .Select(MapToDtoExpression())
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<ResourcePlanDto> CreateAsync(CreateResourcePlanRequest request, CancellationToken cancellationToken = default)
    {
        ValidateMonth(request.StartMonth);
        ValidateVisibleMonths(request.VisibleMonths);

        var entity = new ResourcePlan
        {
            EmployeeId = request.EmployeeId,
            ScenarioId = request.ScenarioId,
            StartYear = request.StartYear,
            StartMonth = request.StartMonth,
            VisibleMonths = request.VisibleMonths,
            Notes = NormalizeNullable(request.Notes),
            IsActive = request.IsActive,
            CreatedBy = NormalizeNullable(request.CreatedBy),
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.ResourcePlans.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapToDto(entity);
    }

    public async Task<ResourcePlanDto?> UpdateAsync(int id, UpdateResourcePlanRequest request, CancellationToken cancellationToken = default)
    {
        ValidateMonth(request.StartMonth);
        ValidateVisibleMonths(request.VisibleMonths);

        var entity = await _dbContext.ResourcePlans
            .FirstOrDefaultAsync(x => x.ResourcePlanId == id, cancellationToken);

        if (entity is null)
        {
            return null;
        }

        entity.EmployeeId = request.EmployeeId;
        entity.ScenarioId = request.ScenarioId;
        entity.StartYear = request.StartYear;
        entity.StartMonth = request.StartMonth;
        entity.VisibleMonths = request.VisibleMonths;
        entity.Notes = NormalizeNullable(request.Notes);
        entity.IsActive = request.IsActive;
        entity.UpdatedBy = NormalizeNullable(request.UpdatedBy);
        entity.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapToDto(entity);
    }

    private static void ValidateMonth(int month)
    {
        if (month < 1 || month > 12)
        {
            throw new InvalidOperationException("StartMonth must be between 1 and 12.");
        }
    }

    private static void ValidateVisibleMonths(int visibleMonths)
    {
        if (visibleMonths <= 0)
        {
            throw new InvalidOperationException("VisibleMonths must be greater than 0.");
        }
    }

    private static string? NormalizeNullable(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static ResourcePlanDto MapToDto(ResourcePlan entity)
    {
        return new ResourcePlanDto
        {
            ResourcePlanId = entity.ResourcePlanId,
            EmployeeId = entity.EmployeeId,
            VirtualResourceId = entity.VirtualResourceId,
            ScenarioId = entity.ScenarioId,
            StartYear = entity.StartYear,
            StartMonth = entity.StartMonth,
            VisibleMonths = entity.VisibleMonths,
            Notes = entity.Notes,
            IsActive = entity.IsActive,
            CreatedBy = entity.CreatedBy,
            UpdatedBy = entity.UpdatedBy,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };
    }

    private static Expression<Func<ResourcePlan, ResourcePlanDto>> MapToDtoExpression()
    {
        return entity => new ResourcePlanDto
        {
            ResourcePlanId = entity.ResourcePlanId,
            EmployeeId = entity.EmployeeId,
            VirtualResourceId = entity.VirtualResourceId,
            ScenarioId = entity.ScenarioId,
            StartYear = entity.StartYear,
            StartMonth = entity.StartMonth,
            VisibleMonths = entity.VisibleMonths,
            Notes = entity.Notes,
            IsActive = entity.IsActive,
            CreatedBy = entity.CreatedBy,
            UpdatedBy = entity.UpdatedBy,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };
    }
}