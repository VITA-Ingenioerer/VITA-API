using Vita.Planning.Application.DTOs;

namespace Vita.Planning.Application.Interfaces;

public interface ISharePointOfferArchiveClient
{
    Task<CreateTilbudssagerFolderResultDto> ResolveYearFolderAsync(
        int year,
        CancellationToken cancellationToken = default);

    Task<CreateTilbudssagerFolderResultDto> CreateOfferFolderAsync(
        CreateTilbudssagerFolderRequest request,
        string createdByUserName,
        CancellationToken cancellationToken = default);
}