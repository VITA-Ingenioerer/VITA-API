using Vita.Atlas.Application.DTOs;

namespace Vita.Atlas.Application.Interfaces;

public interface IEntityChangeLogService
{
    Task<BusinessEventDto> RecordChangeAsync(
        RecordEntityChangeRequest request,
        CancellationToken cancellationToken = default);
}
