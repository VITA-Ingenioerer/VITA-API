using Vita.Planning.Application.DTOs;

namespace Vita.Planning.Application.Interfaces;

public interface IEntityChangeLogService
{
    Task<BusinessEventDto> RecordChangeAsync(
        RecordEntityChangeRequest request,
        CancellationToken cancellationToken = default);
}
