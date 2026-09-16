using Vita.Atlas.Application.DTOs;

namespace Vita.Atlas.Application.Interfaces;

public interface IResourcePlanScenarioService
{
    Task<IReadOnlyList<ResourcePlanScenarioDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<ResourcePlanScenarioDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<ResourcePlanScenarioDto> CreateAsync(CreateResourcePlanScenarioRequest request, CancellationToken cancellationToken = default);
    Task<ResourcePlanScenarioDto?> UpdateAsync(int id, UpdateResourcePlanScenarioRequest request, CancellationToken cancellationToken = default);
}
