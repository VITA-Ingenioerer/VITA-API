using Vita.Planning.Application.DTOs;

namespace Vita.Planning.Application.Interfaces;

public interface IOfferService
{
    Task<PagedResultDto<OfferDto>> GetAllAsync(int page = 1, int pageSize = 100, string? query = null, CancellationToken cancellationToken = default);
    Task<OfferDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<OfferDto> CreateAsync(
        CreateOfferRequest request,
        CallerInfo caller,
        CancellationToken cancellationToken = default);
    Task<OfferDto?> UpdateAsync(
        int id,
        UpdateOfferRequest request,
        CallerInfo caller,
        CancellationToken cancellationToken = default);
    Task<ImportOffersResultDto> ImportAsync(
        Stream fileStream,
        CallerInfo caller,
        CancellationToken cancellationToken = default);

    Task<ConvertOfferToProjectResult> ConvertToProjectAsync(
        int offerId,
        ConvertOfferToProjectRequest request,
        CancellationToken cancellationToken = default);
}
