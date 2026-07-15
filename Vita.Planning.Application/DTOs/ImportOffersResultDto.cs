namespace Vita.Planning.Application.DTOs;

public sealed class ImportOffersResultDto
{
    public int CreatedCount { get; set; }
    public int UpdatedCount { get; set; }
    public int SkippedCount { get; set; }
    public IReadOnlyList<ImportOfferErrorDto> Errors { get; set; } = [];
}

public sealed class ImportOfferErrorDto
{
    public int RowNumber { get; set; }
    public string? OfferNumber { get; set; }
    public string Message { get; set; } = string.Empty;
}
