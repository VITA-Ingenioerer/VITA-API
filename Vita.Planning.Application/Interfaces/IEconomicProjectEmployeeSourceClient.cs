using Vita.Planning.Application.DTOs;

namespace Vita.Planning.Application.Interfaces;

public interface IEconomicProjectEmployeeSourceClient
{
    Task<IReadOnlyList<SourceEconomicProjectEmployeeDto>> GetProjectEmployeesAsync(
        CancellationToken cancellationToken = default);
}