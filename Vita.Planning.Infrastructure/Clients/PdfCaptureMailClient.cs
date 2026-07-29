using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Client;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using Vita.Planning.Application.DTOs;
using Vita.Planning.Application.Interfaces;

namespace Vita.Planning.Infrastructure.Clients;

public sealed class PdfCaptureMailClient : IPdfCaptureMailClient
{
    private const double PageMarginPoints = 20;

    // A4 in points (1/72 inch). Camera captures frequently carry no DPI metadata (or an
    // arbitrary one), so sizing the page directly to the image's reported point dimensions
    // can ask PdfSharpCore for a page far beyond its allowed size and throw. Fitting every
    // capture onto a fixed, standard page instead makes the output a normal, printable
    // document regardless of the source photo's resolution or metadata.
    private const double PageWidthPoints = 595.28;
    private const double PageHeightPoints = 841.89;

    private readonly HttpClient _httpClient;
    private readonly MicrosoftGraphSettings _settings;

    public PdfCaptureMailClient(
        HttpClient httpClient,
        IOptions<MicrosoftGraphSettings> options)
    {
        _httpClient = httpClient;
        _settings = options.Value;
    }

    public async Task<PdfCaptureSendResultDto> SendCapturedImageAsPdfAsync(
        Stream imageStream,
        string recipientEmail,
        string? note,
        CancellationToken cancellationToken = default)
    {
        ValidateSettings();

        if (string.IsNullOrWhiteSpace(recipientEmail))
        {
            throw new InvalidOperationException("Recipient email is required.");
        }

        byte[] pdfBytes;
        try
        {
            pdfBytes = BuildSinglePageImagePdf(imageStream);
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            throw new InvalidOperationException("The uploaded file could not be read as an image.", ex);
        }

        var sentAtUtc = DateTime.UtcNow;
        var fileName = $"Bilag_{sentAtUtc:yyyyMMdd_HHmmss}.pdf";

        var accessToken = await GetAccessTokenAsync(cancellationToken);

        await SendMailAsync(accessToken, recipientEmail, fileName, pdfBytes, note, cancellationToken);

        return new PdfCaptureSendResultDto
        {
            RecipientEmail = recipientEmail,
            FileName = fileName,
            SentAtUtc = sentAtUtc
        };
    }

    private static byte[] BuildSinglePageImagePdf(Stream imageStream)
    {
        using var image = XImage.FromStream(() => imageStream);

        // Landscape photos get a landscape page; everything else (including portrait
        // document scans) gets a standard portrait page.
        var isLandscape = image.PixelWidth > image.PixelHeight;
        var pageWidth = isLandscape ? PageHeightPoints : PageWidthPoints;
        var pageHeight = isLandscape ? PageWidthPoints : PageHeightPoints;

        using var document = new PdfDocument();
        var page = document.AddPage();
        page.Width = XUnit.FromPoint(pageWidth);
        page.Height = XUnit.FromPoint(pageHeight);

        var availableWidth = pageWidth - (2 * PageMarginPoints);
        var availableHeight = pageHeight - (2 * PageMarginPoints);

        var imageAspect = image.PixelWidth / (double)image.PixelHeight;
        var availableAspect = availableWidth / availableHeight;

        double drawWidth, drawHeight;
        if (imageAspect > availableAspect)
        {
            drawWidth = availableWidth;
            drawHeight = availableWidth / imageAspect;
        }
        else
        {
            drawHeight = availableHeight;
            drawWidth = availableHeight * imageAspect;
        }

        var offsetX = PageMarginPoints + ((availableWidth - drawWidth) / 2);
        var offsetY = PageMarginPoints + ((availableHeight - drawHeight) / 2);

        using var gfx = XGraphics.FromPdfPage(page);
        gfx.DrawImage(image, offsetX, offsetY, drawWidth, drawHeight);

        using var output = new MemoryStream();
        document.Save(output, closeStream: false);

        return output.ToArray();
    }

    private async Task SendMailAsync(
        string accessToken,
        string recipientEmail,
        string fileName,
        byte[] pdfBytes,
        string? note,
        CancellationToken cancellationToken)
    {
        var bodyText = string.IsNullOrWhiteSpace(note)
            ? "Vedhæftet bilag er indscannet fra mobilen."
            : $"Vedhæftet bilag er indscannet fra mobilen.\n\nNote: {note}";

        var payload = new GraphSendMailRequest
        {
            Message = new GraphMessage
            {
                Subject = $"Bilag {DateTime.Now:dd-MM-yyyy HH:mm}",
                Body = new GraphItemBody
                {
                    ContentType = "Text",
                    Content = bodyText
                },
                ToRecipients =
                [
                    new GraphRecipient
                    {
                        EmailAddress = new GraphEmailAddress { Address = recipientEmail }
                    }
                ],
                Attachments =
                [
                    new GraphFileAttachment
                    {
                        Name = fileName,
                        ContentType = "application/pdf",
                        ContentBytes = Convert.ToBase64String(pdfBytes)
                    }
                ]
            },
            SaveToSentItems = true
        };

        var requestUrl =
            $"https://graph.microsoft.com/v1.0/users/{Uri.EscapeDataString(recipientEmail)}/sendMail";

        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, requestUrl);
        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        requestMessage.Content = JsonContent.Create(payload);

        using var response = await _httpClient.SendAsync(requestMessage, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);

            throw new InvalidOperationException(
                $"Could not send captured PDF to '{recipientEmail}'. StatusCode: {(int)response.StatusCode} {response.ReasonPhrase}. Body: {errorBody}");
        }
    }

    private async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        var app = ConfidentialClientApplicationBuilder
            .Create(_settings.ClientId)
            .WithClientSecret(_settings.ClientSecret)
            .WithAuthority($"https://login.microsoftonline.com/{_settings.TenantId}")
            .Build();

        var authResult = await app
            .AcquireTokenForClient(["https://graph.microsoft.com/.default"])
            .ExecuteAsync(cancellationToken);

        return authResult.AccessToken;
    }

    private void ValidateSettings()
    {
        if (string.IsNullOrWhiteSpace(_settings.TenantId))
        {
            throw new InvalidOperationException("MicrosoftGraph:TenantId is missing.");
        }

        if (string.IsNullOrWhiteSpace(_settings.ClientId))
        {
            throw new InvalidOperationException("MicrosoftGraph:ClientId is missing.");
        }

        if (string.IsNullOrWhiteSpace(_settings.ClientSecret))
        {
            throw new InvalidOperationException("MicrosoftGraph:ClientSecret is missing.");
        }
    }

    private sealed class GraphSendMailRequest
    {
        [JsonPropertyName("message")]
        public GraphMessage Message { get; init; } = new();

        [JsonPropertyName("saveToSentItems")]
        public bool SaveToSentItems { get; init; }
    }

    private sealed class GraphMessage
    {
        [JsonPropertyName("subject")]
        public string Subject { get; init; } = string.Empty;

        [JsonPropertyName("body")]
        public GraphItemBody Body { get; init; } = new();

        [JsonPropertyName("toRecipients")]
        public List<GraphRecipient> ToRecipients { get; init; } = [];

        [JsonPropertyName("attachments")]
        public List<GraphFileAttachment> Attachments { get; init; } = [];
    }

    private sealed class GraphItemBody
    {
        [JsonPropertyName("contentType")]
        public string ContentType { get; init; } = "Text";

        [JsonPropertyName("content")]
        public string Content { get; init; } = string.Empty;
    }

    private sealed class GraphRecipient
    {
        [JsonPropertyName("emailAddress")]
        public GraphEmailAddress EmailAddress { get; init; } = new();
    }

    private sealed class GraphEmailAddress
    {
        [JsonPropertyName("address")]
        public string Address { get; init; } = string.Empty;
    }

    private sealed class GraphFileAttachment
    {
        [JsonPropertyName("@odata.type")]
        public string ODataType { get; init; } = "#microsoft.graph.fileAttachment";

        [JsonPropertyName("name")]
        public string Name { get; init; } = string.Empty;

        [JsonPropertyName("contentType")]
        public string ContentType { get; init; } = string.Empty;

        [JsonPropertyName("contentBytes")]
        public string ContentBytes { get; init; } = string.Empty;
    }
}
