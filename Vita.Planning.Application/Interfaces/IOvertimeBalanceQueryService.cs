using Vita.Planning.Application.DTOs;

namespace Vita.Planning.Application.Interfaces;

public interface IOvertimeBalanceQueryService
{
    Task<IReadOnlyList<OvertimeBalanceDayDto>> GetDailyBalanceAsync(
        int employeeId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default);

    Task<OvertimeBalanceSummaryDto?> GetCurrentBalanceAsync(
        int employeeId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OvertimeBalanceSummaryDto>> GetCurrentBalancesAsync(
        IReadOnlyCollection<int>? employeeIds, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OvertimeTrendPointDto>> GetTrendAsync(
        DateOnly from, DateOnly to, IReadOnlyCollection<int>? employeeIds, CancellationToken cancellationToken = default);
}
