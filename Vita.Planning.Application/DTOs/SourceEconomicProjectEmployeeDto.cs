namespace Vita.Planning.Application.DTOs;

public sealed class SourceEconomicProjectEmployeeDto
{
    public int Number { get; set; }
    public string Name { get; set; } = string.Empty;
    public int? GroupNumber { get; set; }
    public bool? CanApprove { get; set; }
    public bool? CanInvoice { get; set; }
    public bool? IsBarred { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? ZipCode { get; set; }
    public decimal? CostPriceAfter { get; set; }
    public decimal? CostPriceBefore { get; set; }
    public decimal? SalesPriceAfter { get; set; }
    public decimal? SalesPriceBefore { get; set; }
    public DateTime? CutoffDate { get; set; }
    public string? ObjectVersion { get; set; }
}