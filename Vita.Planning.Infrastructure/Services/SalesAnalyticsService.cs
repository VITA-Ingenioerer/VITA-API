using Microsoft.EntityFrameworkCore;
using Vita.Planning.Application.DTOs;
using Vita.Planning.Application.Interfaces;
using Vita.Planning.Infrastructure.Data;

namespace Vita.Planning.Infrastructure.Services;

public sealed class SalesAnalyticsService : ISalesAnalyticsService
{
    // The one status code that means "withdrawn/removed" — see
    // LegacyImportController's normalization of "removed"/"fjern"/"fjernet" to
    // this same code. Not a guess: it's the same classification the offer list's
    // excludeFjern filter uses.
    private const string LostStatusCode = "Fjern";

    private readonly PlanningDbContext _db;

    // Matches ProjectAdminPage.tsx's own offerStatusVariant()/handleSaveOffer()
    // heuristics: a status can say "Vundet"/"Tabt" before (or without ever)
    // ConvertedToProjectNumber getting set, so text-matching the status name is
    // how the rest of the app already tells won/lost apart, not a guess.
    private static bool StatusNameContains(string statusName, params string[] needles) =>
        needles.Any(needle => statusName.Contains(needle, StringComparison.OrdinalIgnoreCase));

    public SalesAnalyticsService(PlanningDbContext db)
    {
        _db = db;
    }

    private sealed record OfferRow(
        int OfferId,
        string OfferNumber,
        string Title,
        decimal? FeeAmount,
        decimal? ProbabilityPercent,
        int? ExpectedStartYear,
        int? ExpectedStartQuarter,
        DateTime CreatedAtUtc,
        DateTime? ConvertedAtUtc,
        int? ConvertedToProjectNumber,
        string? ResponsibleInitials,
        string? ResponsibleOfficeCode,
        string? ProjectRegion,
        string? CustomerName,
        bool AddToPqCompetition,
        bool? DeliveredToPq,
        DateOnly? PqSubmissionDate,
        DateOnly? EstimatedCompetitionStartDate,
        string? StatusCode,
        string StatusName);

    public async Task<SalesAnalyticsDto> GetSalesAnalyticsAsync(SalesAnalyticsFilterRequest? filter = null, CancellationToken cancellationToken = default)
    {
        var allRows = await _db.Offers
            .AsNoTracking()
            .GroupJoin(_db.OfferStatuses.AsNoTracking(),
                o => o.OfferStatusId, s => s.OfferStatusId,
                (o, statuses) => new { o, statuses })
            .SelectMany(x => x.statuses.DefaultIfEmpty(), (x, status) => new { x.o, status })
            .GroupJoin(_db.Customers.AsNoTracking(),
                x => x.o.CustomerId, c => c.CustomerId,
                (x, customers) => new { x.o, x.status, customers })
            .SelectMany(x => x.customers.DefaultIfEmpty(), (x, customer) => new OfferRow(
                x.o.OfferId,
                x.o.OfferNumber,
                x.o.Title,
                x.o.FeeAmount,
                x.o.ProbabilityPercent,
                x.o.ExpectedStartYear,
                x.o.ExpectedStartQuarter,
                x.o.CreatedAtUtc,
                x.o.ConvertedAtUtc,
                x.o.ConvertedToProjectNumber,
                x.o.ResponsibleInitials,
                x.o.ResponsibleOfficeCode,
                x.o.ProjectRegion,
                customer == null ? null : customer.Name,
                x.o.AddToPqCompetition,
                x.o.DeliveredToPq,
                x.o.PqSubmissionDate,
                x.o.EstimatedCompetitionStartDate,
                x.status == null ? null : x.status.Code,
                x.status == null ? "Ukendt" : x.status.Name))
            .ToListAsync(cancellationToken);

        // Filter dropdown options always come from the FULL unfiltered set —
        // picking "Odense" shouldn't make every other office disappear from
        // its own picker.
        var availableOfficeCodes = allRows
            .Select(r => r.ResponsibleOfficeCode)
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Select(code => code!.Trim())
            .Distinct()
            .OrderBy(code => code)
            .ToList();

        var availableRegions = allRows
            .Select(r => r.ProjectRegion)
            .Where(region => !string.IsNullOrWhiteSpace(region))
            .Select(region => region!.Trim())
            .Distinct()
            .OrderBy(region => region)
            .ToList();

        var availableYears = allRows
            .Select(r => r.CreatedAtUtc.Year)
            .Distinct()
            .OrderByDescending(year => year)
            .ToList();

        var rows = allRows.AsEnumerable();
        if (filter?.Year is { } year)
        {
            rows = rows.Where(r => r.CreatedAtUtc.Year == year);
        }
        if (!string.IsNullOrWhiteSpace(filter?.OfficeCode))
        {
            var normalizedOffice = filter.OfficeCode.Trim();
            rows = rows.Where(r => string.Equals(r.ResponsibleOfficeCode?.Trim(), normalizedOffice, StringComparison.OrdinalIgnoreCase));
        }
        if (!string.IsNullOrWhiteSpace(filter?.Region))
        {
            var normalizedRegion = filter.Region.Trim();
            rows = rows.Where(r => string.Equals(r.ProjectRegion?.Trim(), normalizedRegion, StringComparison.OrdinalIgnoreCase));
        }
        var filteredRows = rows.ToList();

        var summary = await BuildSummaryAsync(filteredRows, filter, cancellationToken);

        return new SalesAnalyticsDto
        {
            Summary = summary,
            ByStatus = BuildStatusBreakdown(filteredRows),
            PipelineByQuarter = BuildPipelineByQuarter(filteredRows),
            ActivityByMonth = BuildActivityByMonth(filteredRows),
            ByResponsible = BuildResponsibleBreakdown(filteredRows),
            ByRegion = BuildRegionBreakdown(filteredRows),
            PqFunnel = BuildPqFunnel(filteredRows),
            RelationalSales = BuildSalesMotionSummary(filteredRows, isTender: false),
            TenderSales = BuildSalesMotionSummary(filteredRows, isTender: true),
            ByCustomer = BuildCustomerBreakdown(filteredRows),
            UpcomingMilestones = BuildUpcomingMilestones(filteredRows),
            ByOffice = BuildOfficeBreakdown(filteredRows),
            AvailableOfficeCodes = availableOfficeCodes,
            AvailableRegions = availableRegions,
            AvailableYears = availableYears,
        };
    }

    private static bool IsWon(OfferRow row) =>
        row.ConvertedToProjectNumber.HasValue || StatusNameContains(row.StatusName, "vundet", "won");

    private static bool IsLost(OfferRow row) =>
        !IsWon(row) && (row.StatusCode == LostStatusCode || StatusNameContains(row.StatusName, "tabt", "lost"));

    // AddToPqCompetition is the one field that distinguishes a competitive
    // tender pursuit from a direct/relationship-driven one — see
    // SalesMotionSummaryDto for exactly what is and isn't derivable per motion.
    private static bool IsTenderMotion(OfferRow row) => row.AddToPqCompetition;

    private static decimal Value(OfferRow row) => row.FeeAmount ?? 0m;

    // A missing probability means "certain" in the planning model (same
    // convention as the resource-plan weighting fix) — only an explicit,
    // recorded percentage should ever discount a value below its face amount.
    private static decimal WeightedValue(OfferRow row) => Value(row) * ((row.ProbabilityPercent ?? 100m) / 100m);

    private async Task<SalesAnalyticsSummaryDto> BuildSummaryAsync(List<OfferRow> rows, SalesAnalyticsFilterRequest? filter, CancellationToken cancellationToken)
    {
        var won = rows.Where(IsWon).ToList();
        var lost = rows.Where(IsLost).ToList();
        var open = rows.Where(r => !IsWon(r) && !IsLost(r)).ToList();

        var projectsQuery = _db.Projects.AsNoTracking().AsQueryable();
        var projects = await projectsQuery
            .Select(p => new { p.ProjectNumber, p.IsClosed })
            .ToListAsync(cancellationToken);

        var openProjectNumbers = projects.Where(p => !p.IsClosed).Select(p => p.ProjectNumber).ToHashSet();

        var openBacklogValue = openProjectNumbers.Count == 0
            ? 0m
            : await _db.ProjectMetadata.AsNoTracking()
                .Where(m => openProjectNumbers.Contains(m.ProjectNumber) && m.BudgetRevenue.HasValue)
                .SumAsync(m => m.BudgetRevenue!.Value, cancellationToken);

        var resolvedCount = won.Count + lost.Count;

        return new SalesAnalyticsSummaryDto
        {
            TotalOffers = rows.Count,
            OpenOffers = open.Count,
            WonOffers = won.Count,
            LostOffers = lost.Count,
            WinRatePercent = resolvedCount == 0 ? 0 : Math.Round(won.Count * 100m / resolvedCount, 1),
            WonValue = won.Sum(Value),
            OpenPipelineValue = open.Sum(Value),
            WeightedOpenPipelineValue = open.Sum(WeightedValue),
            TotalProjects = projects.Count,
            OpenProjects = openProjectNumbers.Count,
            ClosedProjects = projects.Count - openProjectNumbers.Count,
            OpenProjectBacklogValue = openBacklogValue,
        };
    }

    private static IReadOnlyList<OfferStatusBreakdownItemDto> BuildStatusBreakdown(List<OfferRow> rows) =>
        rows
            .GroupBy(r => r.StatusName)
            .Select(g => new OfferStatusBreakdownItemDto
            {
                StatusName = g.Key,
                Count = g.Count(),
                TotalValue = g.Sum(Value),
            })
            .OrderByDescending(x => x.Count)
            .ToList();

    private static IReadOnlyList<QuarterPipelineItemDto> BuildPipelineByQuarter(List<OfferRow> rows) =>
        rows
            .Where(r => r.ExpectedStartYear.HasValue && r.ExpectedStartQuarter.HasValue)
            .GroupBy(r => (r.ExpectedStartYear!.Value, r.ExpectedStartQuarter!.Value))
            .Select(g =>
            {
                var won = g.Where(IsWon).ToList();
                var lost = g.Where(IsLost).ToList();
                var open = g.Where(r => !IsWon(r) && !IsLost(r)).ToList();

                return new QuarterPipelineItemDto
                {
                    Year = g.Key.Item1,
                    Quarter = g.Key.Item2,
                    WonCount = won.Count,
                    WonValue = won.Sum(Value),
                    OpenCount = open.Count,
                    OpenValue = open.Sum(Value),
                    WeightedOpenValue = open.Sum(WeightedValue),
                    LostCount = lost.Count,
                    LostValue = lost.Sum(Value),
                };
            })
            .OrderBy(x => x.Year).ThenBy(x => x.Quarter)
            .ToList();

    private static IReadOnlyList<MonthlyTrendItemDto> BuildActivityByMonth(List<OfferRow> rows)
    {
        var createdByKey = rows
            .GroupBy(r => (r.CreatedAtUtc.Year, r.CreatedAtUtc.Month))
            .ToDictionary(g => g.Key, g => g.Count());

        var wonByKey = rows
            .Where(r => r.ConvertedAtUtc.HasValue)
            .GroupBy(r => (r.ConvertedAtUtc!.Value.Year, r.ConvertedAtUtc.Value.Month))
            .ToDictionary(g => g.Key, g => g.Count());

        var allKeys = createdByKey.Keys.Concat(wonByKey.Keys).Distinct();

        return allKeys
            .Select(key => new MonthlyTrendItemDto
            {
                Year = key.Year,
                Month = key.Month,
                OffersCreated = createdByKey.GetValueOrDefault(key),
                OffersWon = wonByKey.GetValueOrDefault(key),
            })
            .OrderBy(x => x.Year).ThenBy(x => x.Month)
            .ToList();
    }

    private static IReadOnlyList<ResponsibleBreakdownItemDto> BuildResponsibleBreakdown(List<OfferRow> rows) =>
        rows
            .GroupBy(r => string.IsNullOrWhiteSpace(r.ResponsibleInitials) ? "Ukendt" : r.ResponsibleInitials.Trim())
            .Select(g =>
            {
                var won = g.Where(IsWon).ToList();
                var lost = g.Where(IsLost).ToList();
                var resolvedCount = won.Count + lost.Count;

                return new ResponsibleBreakdownItemDto
                {
                    ResponsibleInitials = g.Key,
                    TotalOffers = g.Count(),
                    WonOffers = won.Count,
                    WinRatePercent = resolvedCount == 0 ? 0 : Math.Round(won.Count * 100m / resolvedCount, 1),
                    TotalValue = g.Sum(Value),
                    WonValue = won.Sum(Value),
                };
            })
            .OrderByDescending(x => x.TotalValue)
            .Take(15)
            .ToList();

    private static IReadOnlyList<RegionBreakdownItemDto> BuildRegionBreakdown(List<OfferRow> rows) =>
        rows
            .GroupBy(r => string.IsNullOrWhiteSpace(r.ProjectRegion) ? "Ukendt" : r.ProjectRegion.Trim())
            .Select(g => new RegionBreakdownItemDto
            {
                Region = g.Key,
                Count = g.Count(),
                TotalValue = g.Sum(Value),
            })
            .OrderByDescending(x => x.Count)
            .Take(10)
            .ToList();

    // Each stage is a strict subset of the one before it — entering the PQ
    // competition is the funnel's actual starting point, not "every offer that
    // ever existed", so the stages are always monotonically decreasing.
    private static PqFunnelDto BuildPqFunnel(List<OfferRow> rows)
    {
        var enteredPq = rows.Where(r => r.AddToPqCompetition).ToList();
        var delivered = enteredPq.Where(r => r.DeliveredToPq == true).ToList();
        var won = delivered.Where(IsWon).ToList();

        return new PqFunnelDto
        {
            TotalOffers = enteredPq.Count,
            AddedToPqCompetition = enteredPq.Count,
            DeliveredToPq = delivered.Count,
            Won = won.Count,
        };
    }

    private static SalesMotionSummaryDto BuildSalesMotionSummary(List<OfferRow> rows, bool isTender)
    {
        var motionRows = rows.Where(r => IsTenderMotion(r) == isTender).ToList();
        var won = motionRows.Where(IsWon).ToList();
        var lost = motionRows.Where(IsLost).ToList();
        var resolvedCount = won.Count + lost.Count;

        var summary = new SalesMotionSummaryDto
        {
            MotionName = isTender ? "Udbudsdrevet salg" : "Relationelt salg",
            TilbudCount = motionRows.Count,
            TilbudValue = motionRows.Sum(Value),
            WonCount = won.Count,
            WonValue = won.Sum(Value),
            LostCount = lost.Count,
            LostValue = lost.Sum(Value),
            WinRatePercent = resolvedCount == 0 ? 0 : Math.Round(won.Count * 100m / resolvedCount, 1),
        };

        if (isTender)
        {
            var delivered = motionRows.Where(r => r.DeliveredToPq == true).ToList();
            var notPrequalified = motionRows.Where(r => r.DeliveredToPq != true).ToList();

            summary.TeamFormationCount = motionRows.Count;
            summary.TeamFormationValue = motionRows.Sum(Value);
            summary.PqDeliveredCount = delivered.Count;
            summary.PqDeliveredValue = delivered.Sum(Value);
            summary.NotPrequalifiedCount = notPrequalified.Count;
            summary.NotPrequalifiedValue = notPrequalified.Sum(Value);
        }
        // LeadCount/QualifiedOpportunityCount deliberately left null for the
        // relational motion — see SalesMotionSummaryDto's remarks.

        return summary;
    }

    private static IReadOnlyList<CustomerBreakdownItemDto> BuildCustomerBreakdown(List<OfferRow> rows) =>
        rows
            .Where(IsWon)
            .Where(r => !string.IsNullOrWhiteSpace(r.CustomerName))
            .GroupBy(r => r.CustomerName!.Trim())
            .Select(g => new CustomerBreakdownItemDto
            {
                CustomerName = g.Key,
                WonValue = g.Sum(Value),
                WonCount = g.Count(),
            })
            .OrderByDescending(x => x.WonValue)
            .Take(10)
            .ToList();

    private static IReadOnlyList<UpcomingMilestoneDto> BuildUpcomingMilestones(List<OfferRow> rows)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var openRows = rows.Where(r => !IsWon(r) && !IsLost(r));
        var milestones = new List<UpcomingMilestoneDto>();

        foreach (var row in openRows)
        {
            if (row.PqSubmissionDate.HasValue && row.PqSubmissionDate.Value >= today)
            {
                milestones.Add(new UpcomingMilestoneDto
                {
                    OfferId = row.OfferId,
                    OfferNumber = row.OfferNumber,
                    Title = row.Title,
                    MilestoneType = "PQ-frist",
                    Date = row.PqSubmissionDate.Value,
                    ResponsibleInitials = row.ResponsibleInitials,
                });
            }

            if (row.EstimatedCompetitionStartDate.HasValue && row.EstimatedCompetitionStartDate.Value >= today)
            {
                milestones.Add(new UpcomingMilestoneDto
                {
                    OfferId = row.OfferId,
                    OfferNumber = row.OfferNumber,
                    Title = row.Title,
                    MilestoneType = "Forventet konkurrencestart",
                    Date = row.EstimatedCompetitionStartDate.Value,
                    ResponsibleInitials = row.ResponsibleInitials,
                });
            }
        }

        return milestones
            .OrderBy(m => m.Date)
            .Take(10)
            .ToList();
    }

    private static IReadOnlyList<OfficeBreakdownItemDto> BuildOfficeBreakdown(List<OfferRow> rows) =>
        rows
            .GroupBy(r => string.IsNullOrWhiteSpace(r.ResponsibleOfficeCode) ? "Ukendt" : r.ResponsibleOfficeCode.Trim())
            .Select(g =>
            {
                var won = g.Where(IsWon).ToList();
                var open = g.Where(r => !IsWon(r) && !IsLost(r)).ToList();

                return new OfficeBreakdownItemDto
                {
                    OfficeCode = g.Key,
                    WonValue = won.Sum(Value),
                    ForecastValue = won.Sum(Value) + open.Sum(WeightedValue),
                };
            })
            .OrderByDescending(x => x.WonValue)
            .ToList();
}
