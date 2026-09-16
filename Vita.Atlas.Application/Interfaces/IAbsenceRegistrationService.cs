using Vita.Atlas.Application.DTOs;

namespace Vita.Atlas.Application.Interfaces;

public interface IAbsenceRegistrationService
{
    /// <summary>
    /// Lists e-conomic time registrations booked against the absence ("Fravær") project
    /// within the given date range — the actual bookings, not the internal resource-plan
    /// forecast. Optionally narrowed to one employee.
    /// </summary>
    Task<IReadOnlyList<AbsenceRegistrationDto>> GetAbsenceRegistrationsAsync(
        DateOnly fromDate,
        DateOnly toDate,
        int? employeeId = null,
        CancellationToken cancellationToken = default);
}
