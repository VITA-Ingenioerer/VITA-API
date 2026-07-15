using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Vita.Planning.Application.DTOs;
using Vita.Planning.Application.Interfaces;
using Vita.Planning.Infrastructure.Data;

namespace Vita.Planning.Api.Controllers;

[ApiController]
[Route("api/outlook-tilbudssager")]
public sealed class OutlookTilbudssagerController : ControllerBase
{
    private readonly IOutlookTilbudssagerClient _client;
    private readonly PlanningDbContext _db;

    public OutlookTilbudssagerController(IOutlookTilbudssagerClient client, PlanningDbContext db)
    {
        _client = client;
        _db = db;
    }

    [HttpPost("folders")]
    public async Task<ActionResult<CreateOutlookTilbudssagerFolderResultDto>> CreateFolder(
        [FromBody] CreateOutlookTilbudssagerFolderRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _client.CreateOfferFolderAsync(
                request,
                cancellationToken);

            if (request.OfferId.HasValue)
            {
                try
                {
                    await SaveOfferOutlookDetailsAsync(request.OfferId.Value, result, cancellationToken);
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

    private async Task SaveOfferOutlookDetailsAsync(
        int offerId,
        CreateOutlookTilbudssagerFolderResultDto result,
        CancellationToken cancellationToken)
    {
        var offer = await _db.Offers
            .FirstOrDefaultAsync(o => o.OfferId == offerId, cancellationToken);

        if (offer is null)
            return;

        offer.OfferCaseOutlookFolderId = result.FolderId;
        offer.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
    }
}
