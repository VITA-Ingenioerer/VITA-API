using Vita.Planning.Application.DTOs;

namespace Vita.Planning.Application.Interfaces;

public interface IOvertimeAdjustmentService
{
    Task<IReadOnlyList<OvertimeAdjustmentDto>> GetForEmployeeAsync(
        int employeeId, CancellationToken cancellationToken = default);

    Task<OvertimeAdjustmentDto> CreateAsync(
        CreateOvertimeAdjustmentRequest request, CancellationToken cancellationToken = default);
}
