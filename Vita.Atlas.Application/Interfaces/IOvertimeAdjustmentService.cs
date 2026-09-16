using Vita.Atlas.Application.DTOs;

namespace Vita.Atlas.Application.Interfaces;

public interface IOvertimeAdjustmentService
{
    Task<IReadOnlyList<OvertimeAdjustmentDto>> GetForEmployeeAsync(
        int employeeId, CancellationToken cancellationToken = default);

    Task<OvertimeAdjustmentDto> CreateAsync(
        CreateOvertimeAdjustmentRequest request, CancellationToken cancellationToken = default);
}
