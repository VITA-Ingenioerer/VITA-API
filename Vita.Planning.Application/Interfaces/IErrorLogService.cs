using Vita.Planning.Application.DTOs;

namespace Vita.Planning.Application.Interfaces;

public interface IErrorLogService
{
    Task<OpsErrorDto> LogAsync(RecordOpsErrorRequest request, CancellationToken cancellationToken = default);

    Task<PagedResultDto<OpsErrorDto>> QueryAsync(
        Guid? correlationId = null,
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default);
}
