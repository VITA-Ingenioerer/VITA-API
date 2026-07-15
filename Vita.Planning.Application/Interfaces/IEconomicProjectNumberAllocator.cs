namespace Vita.Planning.Application.Interfaces;

public interface IEconomicProjectNumberAllocator
{
    /// <summary>
    /// Allocates the next available main-project number for the current year against
    /// live e-conomic data and creates it, automatically retrying with the next block (+100)
    /// if e-conomic reports the candidate number already exists. Returns the number actually created.
    /// </summary>
    Task<int> CreateMainProjectAsync(
        string name,
        int projectGroupNumber,
        int? customerNumber,
        int? responsibleEmployeeNumber,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a sub-project starting at mainProjectNumber + preferredOffset (offsets 1..99 within
    /// the main project's block), retrying at the next offset if e-conomic reports a collision.
    /// Returns the sub-project number actually created.
    /// </summary>
    Task<int> CreateSubProjectAsync(
        int mainProjectNumber,
        int preferredOffset,
        string name,
        int projectGroupNumber,
        int? customerNumber,
        int? responsibleEmployeeNumber,
        CancellationToken cancellationToken = default);
}
