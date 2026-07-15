namespace Vita.Planning.Application.Interfaces;

public interface IEconomicProjectWriteClient
{
    /// <summary>
    /// Creates a project in e-conomic with the given project number.
    /// The caller is responsible for supplying a valid, unused number.
    /// </summary>
    Task CreateProjectAsync(
        int projectNumber,
        string name,
        int projectGroupNumber,
        int? customerNumber,
        int? responsibleEmployeeNumber,
        bool isMainProject,
        int? mainProjectNumber,
        CancellationToken cancellationToken = default);
}
