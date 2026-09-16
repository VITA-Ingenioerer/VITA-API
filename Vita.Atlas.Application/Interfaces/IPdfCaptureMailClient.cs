using Vita.Atlas.Application.DTOs;

namespace Vita.Atlas.Application.Interfaces;

public interface IPdfCaptureMailClient
{
    Task<PdfCaptureSendResultDto> SendCapturedImageAsPdfAsync(
        Stream imageStream,
        string recipientEmail,
        string? note,
        CancellationToken cancellationToken = default);
}
