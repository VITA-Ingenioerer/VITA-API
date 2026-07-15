using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using Vita.Planning.Application.DTOs;
using Vita.Planning.Application.Interfaces;
using Vita.Planning.Infrastructure.Data;
using Vita.Planning.Infrastructure.Data.Entities;

namespace Vita.Planning.Infrastructure.Services;

public sealed class ResourcePlanScenarioService : IResourcePlanScenarioService
{
    private readonly PlanningDbContext _dbContext;

    public ResourcePlanScenarioService(PlanningDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<ResourcePlanScenarioDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.ResourcePlanScenarios
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(MapToDtoExpression())
            .ToListAsync(cancellationToken);
    }

    public async Task<ResourcePlanScenarioDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.ResourcePlanScenarios
            .AsNoTracking()
            .Where(x => x.ScenarioId == id)
            .Select(MapToDtoExpression())
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<ResourcePlanScenarioDto> CreateAsync(CreateResourcePlanScenarioRequest request, CancellationToken cancellationToken = default)
    {
        var entity = new ResourcePlanScenario
        {
            Name = request.Name.Trim(),
            Description = NormalizeNullable(request.Description),
            IsDefault = request.IsDefault,
            IsLocked = request.IsLocked,
            CreatedBy = NormalizeNullable(request.CreatedBy),
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.ResourcePlanScenarios.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapToDto(entity);
    }

    public async Task<ResourcePlanScenarioDto?> UpdateAsync(int id, UpdateResourcePlanScenarioRequest request, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.ResourcePlanScenarios
            .FirstOrDefaultAsync(x => x.ScenarioId == id, cancellationToken);

        if (entity is null)
        {
            return null;
        }

        entity.Name = request.Name.Trim();
        entity.Description = NormalizeNullable(request.Description);
        entity.IsDefault = request.IsDefault;
        entity.IsLocked = request.IsLocked;
        entity.UpdatedBy = NormalizeNullable(request.UpdatedBy);
        entity.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapToDto(entity);
    }

    private static string? NormalizeNullable(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static ResourcePlanScenarioDto MapToDto(ResourcePlanScenario entity) => new()
    {
        ScenarioId = entity.ScenarioId,
        Name = entity.Name,
        Description = entity.Description,
        IsDefault = entity.IsDefault,
        IsLocked = entity.IsLocked,
        CreatedBy = entity.CreatedBy,
        UpdatedBy = entity.UpdatedBy,
        CreatedAt = entity.CreatedAt,
        UpdatedAt = entity.UpdatedAt
    };

    private static Expression<Func<ResourcePlanScenario, ResourcePlanScenarioDto>> MapToDtoExpression()
    {
        return x => new ResourcePlanScenarioDto
        {
            ScenarioId = x.ScenarioId,
            Name = x.Name,
            Description = x.Description,
            IsDefault = x.IsDefault,
            IsLocked = x.IsLocked,
            CreatedBy = x.CreatedBy,
            UpdatedBy = x.UpdatedBy,
            CreatedAt = x.CreatedAt,
            UpdatedAt = x.UpdatedAt
        };
    }
}
