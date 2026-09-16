namespace Vita.Atlas.Application.DTOs;

public sealed class SalesAnalyticsDto
{
    public SalesAnalyticsSummaryDto Summary { get; set; } = new();
    public IReadOnlyList<OfferStatusBreakdownItemDto> ByStatus { get; set; } = [];
    public IReadOnlyList<QuarterPipelineItemDto> PipelineByQuarter { get; set; } = [];
    public IReadOnlyList<MonthlyTrendItemDto> ActivityByMonth { get; set; } = [];
    public IReadOnlyList<ResponsibleBreakdownItemDto> ByResponsible { get; set; } = [];
    public IReadOnlyList<RegionBreakdownItemDto> ByRegion { get; set; } = [];
    public PqFunnelDto PqFunnel { get; set; } = new();

    // Added to match the "Relationelt salg" / "Udbudsdrevet salg" split from
    // Peter's Power BI mockup — see SalesMotionSummaryDto for exactly which
    // of its stages are real data versus not tracked yet.
    public SalesMotionSummaryDto RelationalSales { get; set; } = new();
    public SalesMotionSummaryDto TenderSales { get; set; } = new();
    public IReadOnlyList<CustomerBreakdownItemDto> ByCustomer { get; set; } = [];
    public IReadOnlyList<UpcomingMilestoneDto> UpcomingMilestones { get; set; } = [];
    public IReadOnlyList<OfficeBreakdownItemDto> ByOffice { get; set; } = [];
    public IReadOnlyList<string> AvailableOfficeCodes { get; set; } = [];
    public IReadOnlyList<string> AvailableRegions { get; set; } = [];
    public IReadOnlyList<int> AvailableYears { get; set; } = [];
}

public sealed class SalesAnalyticsSummaryDto
{
    public int TotalOffers { get; set; }
    public int OpenOffers { get; set; }
    public int WonOffers { get; set; }
    public int LostOffers { get; set; }
    public decimal WinRatePercent { get; set; }
    public decimal WonValue { get; set; }
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

// One funnel per sales motion, per Peter's mockup. IMPORTANT: not every stage
// he asked for exists as real data today —
//   - Tender ("Udbudsdrevet"): Holddannelser/PQ/Tilbud/Vundet + "Ikke
//     prækvalificeret" are all real, derived from AddToPqCompetition,
//     DeliveredToPq and the same won/lost classification used everywhere
//     else. "Fravalgt (Go/No-Go)" is NOT derivable — there's no field that
//     distinguishes "considered a tender and declined it" from "was never a
//     tender case at all"; both look identical (AddToPqCompetition = false).
//     Adding that distinction would need a new flag captured at Go/No-Go time.
//   - Relational ("Relationelt"): only Tilbud -> Vundet/Tabt is real. "Leads"
//     and "Kvalificerede muligheder" have no backing data at all — there is
//     no lead/opportunity record before an Offer exists. Reporting those
//     would require an actual CRM-style lead-capture step upstream of the
//     current Offer entity, not just a new column.
// LeadCount/QualifiedOpportunityCount are left null (not zero) specifically
// to signal "not tracked", not "tracked and empty" — the frontend renders
// null stages as an explicit "Ikke tilgængelig" placeholder rather than 0.
public sealed class SalesMotionSummaryDto
{
    public string MotionName { get; set; } = string.Empty;
    public int? LeadCount { get; set; }
    public decimal? LeadValue { get; set; }
    public int? QualifiedOpportunityCount { get; set; }
    public decimal? QualifiedOpportunityValue { get; set; }
    public int? TeamFormationCount { get; set; }
    public decimal? TeamFormationValue { get; set; }
    public int? PqDeliveredCount { get; set; }
    public decimal? PqDeliveredValue { get; set; }
    public int? NotPrequalifiedCount { get; set; }
    public decimal? NotPrequalifiedValue { get; set; }
    public int TilbudCount { get; set; }
    public decimal TilbudValue { get; set; }
    public int WonCount { get; set; }
    public decimal WonValue { get; set; }
    public int LostCount { get; set; }
    public decimal LostValue { get; set; }
    public decimal WinRatePercent { get; set; }
}

public sealed class CustomerBreakdownItemDto
{
    public string CustomerName { get; set; } = string.Empty;
    public decimal WonValue { get; set; }
    public int WonCount { get; set; }
}

public sealed class UpcomingMilestoneDto
{
    public int OfferId { get; set; }
    public string OfferNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string MilestoneType { get; set; } = string.Empty;
    public DateOnly Date { get; set; }
    public string? ResponsibleInitials { get; set; }
}

// Budget is deliberately absent — there's no sales-target data source
// anywhere in this system yet (that's a manually-set number someone would
// need to enter and maintain, not something derivable from offers/projects).
// The frontend shows "Budget" and "Afvigelse" as an explicit placeholder
// rather than a fabricated number.
public sealed class OfficeBreakdownItemDto
{
    public string OfficeCode { get; set; } = string.Empty;
    public decimal WonValue { get; set; }
    public decimal ForecastValue { get; set; }
}
