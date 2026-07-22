using Vita.Planning.Application.DTOs;

namespace Vita.Planning.Application.Interfaces;

public interface IVitaHolidayService
{
    Task<IReadOnlyList<VitaHolidayDto>> GetAllAsync(string? countryCode = null, DateOnly? fromDate = null, DateOnly? toDate = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<VitaHolidayOverrideDto>> GetOverridesAsync(string? countryCode = null, DateOnly? fromDate = null, DateOnly? toDate = null, CancellationToken cancellationToken = default);
    Task<VitaHolidayOverrideDto?> GetOverrideByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<VitaHolidayOverrideDto> CreateOverrideAsync(CreateVitaHolidayOverrideRequest request, CancellationToken cancellationToken = default);
    Task<VitaHolidayOverrideDto?> UpdateOverrideAsync(int id, UpdateVitaHolidayOverrideRequest request, CancellationToken cancellationToken = default);
    Task<VitaHolidayOverrideDto?> DeleteOverrideAsync(int id, CancellationToken cancellationToken = default);
}
