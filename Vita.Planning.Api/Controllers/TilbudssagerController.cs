using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Vita.Planning.Application.DTOs;
using Vita.Planning.Application.Interfaces;
using Vita.Planning.Infrastructure.Data;

namespace Vita.Planning.Api.Controllers;

[ApiController]
[Route("api/tilbudssager")]
public sealed class TilbudssagerController : ControllerBase
{
    private readonly ISharePointOfferArchiveClient _sharePointOfferArchiveClient;
    private readonly PlanningDbContext _db;

    public TilbudssagerController(
        ISharePointOfferArchiveClient sharePointOfferArchiveClient,
        PlanningDbContext db)
    {
        _sharePointOfferArchiveClient = sharePointOfferArchiveClient;
        _db = db;
    }

    [HttpGet("resolve")]
    public async Task<ActionResult<CreateTilbudssagerFolderResultDto>> Resolve(
        [FromQuery] int year,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _sharePointOfferArchiveClient.ResolveYearFolderAsync(
                year,
                cancellationToken);

            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("folders")]
    public async Task<ActionResult<CreateTilbudssagerFolderResultDto>> CreateFolder(
        [FromBody] CreateTilbudssagerFolderRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var caller = CallerInfo.FromClaimsPrincipal(User);
            var userName = ResolveUserName(caller);

            var result = await _sharePointOfferArchiveClient.CreateOfferFolderAsync(
                request,
                userName,
                cancellationToken);

            if (request.OfferId.HasValue)
            {
                try
                {
                    await SaveOfferSharePointDetailsAsync(request.OfferId.Value, result, cancellationToken);
                }
                catch
                {
                    // Best-effort — folder creation still succeeds even if the offer update fails
                }
            }

            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    private async Task SaveOfferSharePointDetailsAsync(
        int offerId,
        CreateTilbudssagerFolderResultDto result,
        CancellationToken cancellationToken)
    {
        var offer = await _db.Offers
            .FirstOrDefaultAsync(o => o.OfferId == offerId, cancellationToken);

        if (offer is null)
            return;

        offer.OfferCaseDriveId = result.DocumentLibraryDriveId;
        offer.OfferCaseFolderItemId = result.OfferFolderItemId;
        offer.OfferCaseUrl = result.OfferFolderWebUrl;
        offer.OfferCasePath = result.OfferFolderPath;
        offer.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
    }

    private static string ResolveUserName(CallerInfo caller)
    {
        var source =
            !string.IsNullOrWhiteSpace(caller.Email)
                ? caller.Email
                : !string.IsNullOrWhiteSpace(caller.UserId)
                    ? caller.UserId
                    : caller.Name;

        var atIndex = source.IndexOf('@');

        if (atIndex > 0)
            source = source[..atIndex];

        return source.Trim().ToUpperInvariant();
    }
}
