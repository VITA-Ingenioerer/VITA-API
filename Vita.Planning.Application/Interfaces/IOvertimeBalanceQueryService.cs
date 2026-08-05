using Vita.Planning.Application.DTOs;

namespace Vita.Planning.Application.Interfaces;

public interface IOvertimeBalanceQueryService
{
    Task<IReadOnlyList<OvertimeBalanceDayDto>> GetDailyBalanceAsync(
        int employeeId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default);

    Task<OvertimeBalanceSummaryDto?> GetCurrentBalanceAsync(
        int employeeId, CancellationToken cancellationToken = default);
}
