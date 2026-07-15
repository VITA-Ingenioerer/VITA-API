using Vita.Planning.Application.DTOs;

namespace Vita.Planning.Application.Interfaces;

public interface IEmployeeCapacityPeriodService
{
    Task<IReadOnlyList<EmployeeCapacityPeriodDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<EmployeeCapacityPeriodDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<EmployeeCapacityPeriodDto> CreateAsync(CreateEmployeeCapacityPeriodRequest request, CancellationToken cancellationToken = default);
    Task<EmployeeCapacityPeriodDto?> UpdateAsync(int id, UpdateEmployeeCapacityPeriodRequest request, CancellationToken cancellationToken = default);
}
