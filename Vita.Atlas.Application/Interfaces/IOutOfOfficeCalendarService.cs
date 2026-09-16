using Vita.Atlas.Application.DTOs;

namespace Vita.Atlas.Application.Interfaces;

public interface IOutOfOfficeCalendarService
{
    Task<OutOfOfficeCalendarEventDto> CreateAsync(
        int employeeId,
        CreateOutOfOfficeCalendarEventRequest request,
        CallerInfo caller,
        CancellationToken cancellationToken = default);
}
