using Vita.Atlas.Application.DTOs;

namespace Vita.Atlas.Application.Interfaces;

public interface IEmployeeCapacityProfileService
{
    Task<IReadOnlyList<EmployeeCapacityProfileDto>> GetAllAsync(int? employeeId = null, bool? isActive = null, CancellationToken cancellationToken = default);
    Task<EmployeeCapacityProfileDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<EmployeeCapacityProfileDto> CreateAsync(CreateEmployeeCapacityProfileRequest request, CancellationToken cancellationToken = default);
    Task<EmployeeCapacityProfileDto?> UpdateAsync(int id, UpdateEmployeeCapacityProfileRequest request, CancellationToken cancellationToken = default);
    Task<EmployeeCapacityProfileDto?> DeleteAsync(int id, CancellationToken cancellationToken = default);
}
