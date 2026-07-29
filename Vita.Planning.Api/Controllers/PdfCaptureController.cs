using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vita.Planning.Application.DTOs;
using Vita.Planning.Application.Interfaces;

namespace Vita.Planning.Api.Controllers;

[ApiController]
[Route("api/pdf-capture")]
[Authorize]
public sealed class PdfCaptureController : ControllerBase
{
    private const long MaxImageSizeBytes = 20 * 1024 * 1024;

    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png"
    };

    private readonly IPdfCaptureMailClient _pdfCaptureMailClient;

    public PdfCaptureController(IPdfCaptureMailClient pdfCaptureMailClient)
    {
        _pdfCaptureMailClient = pdfCaptureMailClient;
    }

    [HttpPost("send")]
    [RequestSizeLimit(MaxImageSizeBytes)]
    public async Task<ActionResult<PdfCaptureSendResultDto>> Send(
        [FromForm] PdfCaptureSendRequest request,
        CancellationToken cancellationToken)
    {
        var image = request.Image;

        if (image is null || image.Length == 0)
        {
            return BadRequest(new { message = "An image file is required." });
        }

        if (image.Length > MaxImageSizeBytes)
        {
            return BadRequest(new { message = "Image exceeds the maximum allowed size of 20 MB." });
        }

        if (!AllowedContentTypes.Contains(image.ContentType))
        {
            return BadRequest(new { message = $"Unsupported image content type '{image.ContentType}'." });
        }

        var caller = CallerInfo.FromClaimsPrincipal(User);

        if (string.IsNullOrWhiteSpace(caller.Email))
        {
            return BadRequest(new { message = "Could not resolve the caller's email address from the token." });
        }

        try
        {
            await using var imageStream = image.OpenReadStream();

            var result = await _pdfCaptureMailClient.SendCapturedImageAsPdfAsync(
                imageStream,
                caller.Email,
                request.Note,
                cancellationToken);

            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}

public sealed class PdfCaptureSendRequest
{
    public IFormFile Image { get; set; } = default!;

    public string? Note { get; set; }
}
