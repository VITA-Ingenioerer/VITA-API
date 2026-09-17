using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using Vita.Atlas.Application.DTOs;
using Vita.Atlas.Application.Interfaces;
using Vita.Atlas.Infrastructure.Data;
using Vita.Atlas.Infrastructure.Data.Entities;

namespace Vita.Atlas.Infrastructure.Services;

public sealed class ResourcePlanService : IResourcePlanService
{
    private readonly AtlasDbContext _dbContext;

    public ResourcePlanService(AtlasDbContext dbContext)
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

        // Exactly one owner. Enforced here rather than by [Required] on either property,
        // because the requirement is a relationship between the two and an attribute can only
        // see one at a time.
        if (request.EmployeeId.HasValue == request.VirtualResourceId.HasValue)
        {
            throw new InvalidOperationException(
                "A resource plan needs exactly one of EmployeeId or VirtualResourceId.");
        }

        if (request.EmployeeId.HasValue)
        {
            var employeeExists = await _dbContext.Users
                .AnyAsync(x => x.EmployeeId == request.EmployeeId.Value, cancellationToken);

            if (!employeeExists)
            {
                throw new InvalidOperationException($"Employee {request.EmployeeId.Value} was not found.");
            }
        }
        else
        {
            var virtualResourceExists = await _dbContext.VirtualResources
                .AnyAsync(x => x.VirtualResourceId == request.VirtualResourceId!.Value, cancellationToken);

            if (!virtualResourceExists)
            {
                throw new InvalidOperationException(
                    $"Virtual resource {request.VirtualResourceId!.Value} was not found.");
            }
        }

        // One plan per owner per scenario; a second would split the same person's hours across
        // two grids. The legacy import resolves rather than creates for the same reason.
        var duplicate = request.EmployeeId.HasValue
            ? await _dbContext.ResourcePlans.AnyAsync(
                x => x.EmployeeId == request.EmployeeId.Value && x.ScenarioId == request.ScenarioId, cancellationToken)
            : await _dbContext.ResourcePlans.AnyAsync(
                x => x.VirtualResourceId == request.VirtualResourceId!.Value && x.ScenarioId == request.ScenarioId, cancellationToken);

        if (duplicate)
        {
            throw new InvalidOperationException(
                "A resource plan already exists for this resource in the given scenario.");
        }

        var entity = new ResourcePlan
        {
            EmployeeId = request.EmployeeId,
            VirtualResourceId = request.VirtualResourceId,
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