using Vita.Planning.Application.DTOs;

namespace Vita.Planning.Application.Interfaces;

public interface IOutlookTilbudssagerClient
{
    Task<CreateOutlookTilbudssagerFolderResultDto> CreateOfferFolderAsync(
        CreateOutlookTilbudssagerFolderRequest request,
        CancellationToken cancellationToken = default);
}