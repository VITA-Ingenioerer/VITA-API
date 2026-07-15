using Vita.Planning.Application.DTOs;

namespace Vita.Planning.Application.Interfaces;

public interface IEmployeeCapacityOverrideService
{
    Task<IReadOnlyList<EmployeeCapacityOverrideDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<EmployeeCapacityOverrideDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<EmployeeCapacityOverrideDto> CreateAsync(CreateEmployeeCapacityOverrideRequest request, CancellationToken cancellationToken = default);
    Task<EmployeeCapacityOverrideDto?> UpdateAsync(int id, UpdateEmployeeCapacityOverrideRequest request, CancellationToken cancellationToken = default);
}
