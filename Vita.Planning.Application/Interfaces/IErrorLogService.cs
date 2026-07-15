using Vita.Planning.Application.DTOs;

namespace Vita.Planning.Application.Interfaces;

public interface IErrorLogService
{
    Task<OpsErrorDto> LogAsync(RecordOpsErrorRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OpsErrorDto>> QueryAsync(
        Guid? correlationId = null,
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        int take = 200,
        CancellationToken cancellationToken = default);
}
