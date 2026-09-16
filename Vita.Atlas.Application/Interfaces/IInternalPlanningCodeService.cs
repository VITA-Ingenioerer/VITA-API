using Vita.Atlas.Application.DTOs;

namespace Vita.Atlas.Application.Interfaces;

public interface IInternalPlanningCodeService
{
    Task<IReadOnlyList<InternalPlanningCodeDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<InternalPlanningCodeDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<InternalPlanningCodeDto> CreateAsync(CreateInternalPlanningCodeRequest request, CancellationToken cancellationToken = default);
    Task<InternalPlanningCodeDto?> UpdateAsync(int id, UpdateInternalPlanningCodeRequest request, CancellationToken cancellationToken = default);
}