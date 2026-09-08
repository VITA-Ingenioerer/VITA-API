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
        decimal? FeeAmount,
        decimal? ProbabilityPercent,
        int? ExpectedStartYear,
        int? ExpectedStartQuarter,
        DateTime CreatedAtUtc,
        DateTime? ConvertedAtUtc,
        int? ConvertedToProjectNumber,
        string? ResponsibleInitials,
        string? ProjectRegion,
        bool AddToPqCompetition,
        bool? DeliveredToPq,
        string? StatusCode,
        string StatusName);

    public async Task<SalesAnalyticsDto> GetSalesAnalyticsAsync(CancellationToken cancellationToken = default)
    {
        var rows = await _db.Offers
            .AsNoTracking()
            .GroupJoin(_db.OfferStatuses.AsNoTracking(),
                o => o.OfferStatusId, s => s.OfferStatusId,
                (o, statuses) => new { o, statuses })
            .SelectMany(x => x.statuses.DefaultIfEmpty(), (x, status) => new OfferRow(
                x.o.FeeAmount,
                x.o.ProbabilityPercent,
                x.o.ExpectedStartYear,
                x.o.ExpectedStartQuarter,
                x.o.CreatedAtUtc,
                x.o.ConvertedAtUtc,
                x.o.ConvertedToProjectNumber,
                x.o.ResponsibleInitials,
                x.o.ProjectRegion,
                x.o.AddToPqCompetition,
                x.o.DeliveredToPq,
                status == null ? null : status.Code,
                status == null ? "Ukendt" : status.Name))
            .ToListAsync(cancellationToken);

        var summary = await BuildSummaryAsync(rows, cancellationToken);

        return new SalesAnalyticsDto
        {
            Summary = summary,
            ByStatus = BuildStatusBreakdown(rows),
            PipelineByQuarter = BuildPipelineByQuarter(rows),
            ActivityByMonth = BuildActivityByMonth(rows),
            ByResponsible = BuildResponsibleBreakdown(rows),
            ByRegion = BuildRegionBreakdown(rows),
            PqFunnel = BuildPqFunnel(rows),
        };
    }

    private static bool IsWon(OfferRow row) =>
        row.ConvertedToProjectNumber.HasValue || StatusNameContains(row.StatusName, "vundet", "won");

    private static bool IsLost(OfferRow row) =>
        !IsWon(row) && (row.StatusCode == LostStatusCode || StatusNameContains(row.StatusName, "tabt", "lost"));

    private static decimal Value(OfferRow row) => row.FeeAmount ?? 0m;

    // A missing probability means "certain" in the planning model (same
    // convention as the resource-plan weighting fix) — only an explicit,
    // recorded percentage should ever discount a value below its face amount.
    private static decimal WeightedValue(OfferRow row) => Value(row) * ((row.ProbabilityPercent ?? 100m) / 100m);

    private async Task<SalesAnalyticsSummaryDto> BuildSummaryAsync(List<OfferRow> rows, CancellationToken cancellationToken)
    {
        var won = rows.Where(IsWon).ToList();
        var lost = rows.Where(IsLost).ToList();
        var open = rows.Where(r => !IsWon(r) && !IsLost(r)).ToList();

        var projects = await _db.Projects.AsNoTracking()
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
}
