namespace Vita.Atlas.Application.DTOs;

/// <summary>
/// The order book: what has been won and not yet delivered.
///
/// Deliberately a separate report from sales-overview, not another section of it. The two answer
/// different questions over different populations and cannot be added together:
///
///   sales-overview   offers    a FLOW, for a period  — what are we winning?
///   backlog-overview projects  a STOCK, as of a date — what have we won and not delivered?
///
/// An opportunity leaves the first and enters the second the moment
/// core.offers.converted_to_project_number is set. Counting it in both — which is what happened
/// while WonValue and OpenProjectBacklogValue lived in one summary — states the same money twice,
/// in two different currencies of measure at that: offers carry fee_amount, projects carry
/// project_metadata.budget_revenue.
///
/// There is no year filter here on purpose. A period filter on a stock is meaningless; a backlog
/// is only ever "as of" a moment, which is what <see cref="AsOfUtc"/> reports.
/// </summary>
public sealed class BacklogAnalyticsDto
{
    public DateTime AsOfUtc { get; set; }

    public BacklogSummaryDto Summary { get; set; } = new();

    public IReadOnlyList<BacklogBreakdownItemDto> ByOffice { get; set; } = [];

    public IReadOnlyList<BacklogBreakdownItemDto> ByRegion { get; set; } = [];

    public IReadOnlyList<string> AvailableOfficeCodes { get; set; } = [];

    public IReadOnlyList<string> AvailableRegions { get; set; } = [];
}

public sealed class BacklogSummaryDto
{
    public int OpenProjectCount { get; set; }

    public int ClosedProjectCount { get; set; }

    public int TotalProjectCount { get; set; }

    /// <summary>Sum of budget_revenue across open projects. See <see cref="OpenProjectsWithoutBudgetCount"/>.</summary>
    public decimal OpenBacklogValue { get; set; }

    /// <summary>
    /// How many open projects carry no budget_revenue at all. Reported rather than hidden: those
    /// projects contribute nothing to the value above, so without this number the backlog looks
    /// precise when it may be missing most of its mass.
    /// </summary>
    public int OpenProjectsWithoutBudgetCount { get; set; }

    /// <summary>
    /// Backlog on projects that came from an offer (project_metadata.original_offer_id is set).
    /// This is exactly the overlap with the sales report — the part that would be double counted
    /// if the two reports were added together.
    /// </summary>
    public decimal BacklogFromOffersValue { get; set; }

    /// <summary>
    /// Backlog on projects with no originating offer. Safe to add to won offers without double
    /// counting, which is the only sound way to state one combined commercial figure.
    /// </summary>
    public decimal BacklogWithoutOfferValue { get; set; }
}

public sealed class BacklogBreakdownItemDto
{
    public string Key { get; set; } = string.Empty;

    public int OpenProjectCount { get; set; }

    public decimal OpenBacklogValue { get; set; }
}

public sealed class BacklogAnalyticsFilterRequest
{
    public string? OfficeCode { get; set; }

    public string? Region { get; set; }
}
