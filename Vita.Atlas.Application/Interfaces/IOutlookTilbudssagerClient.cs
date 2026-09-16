using Vita.Atlas.Application.DTOs;

namespace Vita.Atlas.Application.Interfaces;

public interface IOutlookTilbudssagerClient
{
    Task<CreateOutlookTilbudssagerFolderResultDto> CreateOfferFolderAsync(
        CreateOutlookTilbudssagerFolderRequest request,
        CancellationToken cancellationToken = default);
}