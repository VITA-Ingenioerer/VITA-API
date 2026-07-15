namespace Vita.Planning.Application.DTOs;

public sealed class SourceEconomicProjectGroupDto
{
    public int Number { get; set; }
    public string Name { get; set; } = string.Empty;

    public int TypeNumber { get; set; }

    public int? CostAccountClosed { get; set; }
    public int? CostAccountOngoing { get; set; }
    public int CostAccountOngoingType { get; set; }
    public int? CostContraAccountOngoing { get; set; }

    public int? SalesAccountClosed { get; set; }
    public int? SalesAccountOngoing { get; set; }
    public int SalesAccountOngoingType { get; set; }
    public int? SalesContraAccountOngoing { get; set; }

    public bool? IncludeCostPriceInFinance { get; set; }
    public bool? IncludeSalesPriceInFinance { get; set; }

    public string? ObjectVersion { get; set; }
}