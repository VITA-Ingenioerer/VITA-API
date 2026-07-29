using Vita.Planning.Application.DTOs;

namespace Vita.Planning.Application.Interfaces;

public interface IPdfCaptureMailClient
{
    Task<PdfCaptureSendResultDto> SendCapturedImageAsPdfAsync(
        Stream imageStream,
        string recipientEmail,
        string? note,
        CancellationToken cancellationToken = default);
}
