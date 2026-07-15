using Vita.Planning.Application.DTOs;

namespace Vita.Planning.Application.Interfaces;

/// <summary>
/// Reads the Ressourceplane workbook directly from SharePoint via Microsoft Graph,
/// replacing the old legacy HTTP API as the source for legacy planned-hours import.
/// </summary>
public interface IRessourceplanWorkbookSourceClient
{
    Task<IReadOnlyList<RessourceplanWorkbookRowDto>> GetRowsAsync(CancellationToken cancellationToken = default);
}
