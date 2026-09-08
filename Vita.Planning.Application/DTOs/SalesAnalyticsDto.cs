namespace Vita.Planning.Application.DTOs;

public sealed class SalesAnalyticsDto
{
    public SalesAnalyticsSummaryDto Summary { get; set; } = new();
    public IReadOnlyList<OfferStatusBreakdownItemDto> ByStatus { get; set; } = [];
    public IReadOnlyList<QuarterPipelineItemDto> PipelineByQuarter { get; set; } = [];
    public IReadOnlyList<MonthlyTrendItemDto> ActivityByMonth { get; set; } = [];
    public IReadOnlyList<ResponsibleBreakdownItemDto> ByResponsible { get; set; } = [];
    public IReadOnlyList<RegionBreakdownItemDto> ByRegion { get; set; } = [];
    public PqFunnelDto PqFunnel { get; set; } = new();
}

public sealed class SalesAnalyticsSummaryDto
{
    public int TotalOffers { get; set; }
    public int OpenOffers { get; set; }
    public int WonOffers { get; set; }
    public int LostOffers { get; set; }
    public decimal WinRatePercent { get; set; }
    public decimal OpenPipelineValue { get; set; }
    public decimal WeightedOpenPipelineValue { get; set; }

    public int TotalProjects { get; set; }
    public int OpenProjects { get; set; }
    public int ClosedProjects { get; set; }
    public decimal OpenProjectBacklogValue { get; set; }
}

public sealed class OfferStatusBreakdownItemDto
{
    public string StatusName { get; set; } = string.Empty;
    public int Count { get; set; }
    public decimal TotalValue { get; set; }
}

// One row per (year, quarter) the offer is EXPECTED to start — a schedule view
// of the pipeline, not a look-back. WeightedOpenValue is what feeds the
// probability-weighted forecast; the raw OpenValue is shown alongside it so the
// gap between "asked for" and "realistically expected" is visible, not hidden.
public sealed class QuarterPipelineItemDto
{
    public int Year { get; set; }
    public int Quarter { get; set; }
    public int WonCount { get; set; }
    public decimal WonValue { get; set; }
    public int OpenCount { get; set; }
    public decimal OpenValue { get; set; }
    public decimal WeightedOpenValue { get; set; }
    public int LostCount { get; set; }
    public decimal LostValue { get; set; }
}

// One row per (year, month) something actually HAPPENED — created or won — a
// look-back activity trend, distinct from PipelineByQuarter's forward schedule.
// This is the series the frontend's linear-trend forecast is fitted against.
public sealed class MonthlyTrendItemDto
{
    public int Year { get; set; }
    public int Month { get; set; }
    public int OffersCreated { get; set; }
    public int OffersWon { get; set; }
}

public sealed class ResponsibleBreakdownItemDto
{
    public string ResponsibleInitials { get; set; } = string.Empty;
    public int TotalOffers { get; set; }
    public int WonOffers { get; set; }
    public decimal WinRatePercent { get; set; }
    public decimal TotalValue { get; set; }
    public decimal WonValue { get; set; }
}

public sealed class RegionBreakdownItemDto
{
    public string Region { get; set; } = string.Empty;
    public int Count { get; set; }
    public decimal TotalValue { get; set; }
}

public sealed class PqFunnelDto
{
    public int TotalOffers { get; set; }
    public int AddedToPqCompetition { get; set; }
    public int DeliveredToPq { get; set; }
    public int Won { get; set; }
}
