namespace Vita.Planning.Application.Interfaces;

/// <summary>
/// Best-effort match of a resource plan entry to one of the e-conomic activities actually
/// assigned to its project (ext.project_activities), so descriptions like "HVAC Arbejde" can
/// be linked to a real activity instead of staying free text.
/// </summary>
public interface IActivityMatchingService
{
    /// <summary>
    /// Tries to match, in order: the entry's description against the project's available
    /// activity names, then (if that fails) the employee's department against the same names.
    /// Returns null (never guesses) if neither matches with reasonable confidence, or if the
    /// project has no activities assigned in ext.project_activities.
    /// </summary>
    Task<int?> MatchActivityAsync(
        int planningTargetId,
        string? description,
        int? employeeId,
        CancellationToken cancellationToken = default);
}
