using Vita.Atlas.Application.DTOs;

namespace Vita.Atlas.Application.Interfaces;

public interface IEconomicProjectEmployeeSourceClient
{
    Task<IReadOnlyList<SourceEconomicProjectEmployeeDto>> GetProjectEmployeesAsync(
        CancellationToken cancellationToken = default);
}