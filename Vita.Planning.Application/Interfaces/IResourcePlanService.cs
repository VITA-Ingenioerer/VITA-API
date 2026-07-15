using Vita.Planning.Application.DTOs;

namespace Vita.Planning.Application.Interfaces;

public interface IResourcePlanService
{
    Task<IReadOnlyList<ResourcePlanDto>> GetAllAsync(int? scenarioId = null, CancellationToken cancellationToken = default);
    Task<ResourcePlanDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<ResourcePlanDto> CreateAsync(CreateResourcePlanRequest request, CancellationToken cancellationToken = default);
    Task<ResourcePlanDto?> UpdateAsync(int id, UpdateResourcePlanRequest request, CancellationToken cancellationToken = default);
}