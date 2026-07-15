namespace Vita.Planning.Application.DTOs;

public sealed class SourceEconomicActivityDto
{
    public int Number { get; set; }
    public int ActivityGroupNumber { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal? CostPriceMarkupPercentage { get; set; }
    public DateTime? CutoffDate { get; set; }
    public bool? HideInSearch { get; set; }
    public int InLieuCode { get; set; }
    public bool? IsAccessible { get; set; }
    public decimal? SalesPriceAfter { get; set; }
    public decimal? SalesPriceBefore { get; set; }
    public string? ObjectVersion { get; set; }
}