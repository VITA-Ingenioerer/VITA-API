namespace Vita.Planning.Application.DTOs;

public sealed class PdfCaptureSendResultDto
{
    public string RecipientEmail { get; init; } = string.Empty;

    public string FileName { get; init; } = string.Empty;

    public DateTime SentAtUtc { get; init; }
}
