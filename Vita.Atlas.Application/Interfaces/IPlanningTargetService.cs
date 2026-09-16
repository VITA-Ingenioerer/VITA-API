using Vita.Atlas.Application.DTOs;

namespace Vita.Atlas.Application.Interfaces;

public interface IPlanningTargetService
{
    Task<IReadOnlyList<PlanningTargetDto>> GetAllAsync(bool? isActive = null, CancellationToken cancellationToken = default);
    Task<PlanningTargetDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<PlanningTargetDto> CreateAsync(CreatePlanningTargetRequest request, CancellationToken cancellationToken = default);
    Task<PlanningTargetDto?> UpdateAsync(int id, UpdatePlanningTargetRequest request, CancellationToken cancellationToken = default);
}