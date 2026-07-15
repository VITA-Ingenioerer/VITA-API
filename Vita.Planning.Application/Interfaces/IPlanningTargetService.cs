using Vita.Planning.Application.DTOs;

namespace Vita.Planning.Application.Interfaces;

public interface IPlanningTargetService
{
    Task<IReadOnlyList<PlanningTargetDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<PlanningTargetDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<PlanningTargetDto> CreateAsync(CreatePlanningTargetRequest request, CancellationToken cancellationToken = default);
    Task<PlanningTargetDto?> UpdateAsync(int id, UpdatePlanningTargetRequest request, CancellationToken cancellationToken = default);
}