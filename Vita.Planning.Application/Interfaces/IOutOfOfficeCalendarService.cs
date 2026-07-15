using Vita.Planning.Application.DTOs;

namespace Vita.Planning.Application.Interfaces;

public interface IOutOfOfficeCalendarService
{
    Task<OutOfOfficeCalendarEventDto> CreateAsync(
        int employeeId,
        CreateOutOfOfficeCalendarEventRequest request,
        CallerInfo caller,
        CancellationToken cancellationToken = default);
}
