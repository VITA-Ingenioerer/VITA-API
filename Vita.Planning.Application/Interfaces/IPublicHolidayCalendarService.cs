using Vita.Planning.Application.DTOs;

namespace Vita.Planning.Application.Interfaces;

public interface IPublicHolidayCalendarService
{
    Task<IReadOnlyList<PublicHolidayCalendarDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<PublicHolidayCalendarDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<PublicHolidayCalendarDto> CreateAsync(CreatePublicHolidayCalendarRequest request, CancellationToken cancellationToken = default);
    Task<PublicHolidayCalendarDto?> UpdateAsync(int id, UpdatePublicHolidayCalendarRequest request, CancellationToken cancellationToken = default);
    Task<object> SyncAsync(string countryCode, int year, IReadOnlyList<PublicHolidayCalendarDto> holidays, CancellationToken cancellationToken = default);
}
