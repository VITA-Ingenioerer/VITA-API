namespace Vita.Planning.Application.DTOs;

// A simplification worth stating plainly: "year" filters on CreatedAtUtc's
// year, not a true YTD-as-of-today window against a separate won-date filter.
// Good enough for "which year's cohort of offers" without pretending to be
// exact fiscal-YTD accounting, which the missing budget/target data couldn't
// back up precisely anyway.
public sealed class SalesAnalyticsFilterRequest
{
    public int? Year { get; set; }
    public string? OfficeCode { get; set; }
    public string? Region { get; set; }
}
