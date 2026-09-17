using Microsoft.EntityFrameworkCore;
using Vita.Atlas.Application.DTOs;
using Vita.Atlas.Application.Interfaces;
using Vita.Atlas.Infrastructure.Data;

namespace Vita.Atlas.Infrastructure.Services;

/// <summary>
/// The order book, over projects only. See <see cref="BacklogAnalyticsDto"/> for why this is its
/// own report rather than a section of the sales overview.
/// </summary>
public sealed class BacklogAnalyticsService : IBacklogAnalyticsService
{
    private readonly AtlasDbContext _db;

    public BacklogAnalyticsService(AtlasDbContext db)
    {
        _db = db;
    }

    public async Task<BacklogAnalyticsDto> GetBacklogAsync(
        BacklogAnalyticsFilterRequest? filter,
        CancellationToken cancellationToken = default)
    {
        // Office and region live on project_metadata, not on the project itself, so the join is
        // left — a project with no metadata row still counts toward the project totals. It drops
        // out as soon as either filter is applied, which is correct: an unclassified project
        // cannot be claimed by an office.
        var rows = await _db.Projects
            .AsNoTracking()
            .GroupJoin(
                _db.ProjectMetadata.AsNoTracking(),
                p => p.ProjectNumber,
                m => m.ProjectNumber,
                (p, metas) => new { p, metas })
            .SelectMany(x => x.metas.DefaultIfEmpty(), (x, meta) => new BacklogRow
            {
                ProjectNumber = x.p.ProjectNumber,
                IsClosed = x.p.IsClosed,
                BudgetRevenue = meta == null ? null : meta.BudgetRevenue,
                OfficeCode = meta == null ? null : meta.ResponsibleOfficeCode,
                Region = meta == null ? null : meta.ProjectRegion,
                OriginalOfferId = meta == null ? null : meta.OriginalOfferId,
            })
            .ToListAsync(cancellationToken);

        var availableOfficeCodes = rows
            .Select(r => r.OfficeCode)
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Select(code => code!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(code => code)
            .ToList();

        var availableRegions = rows
            .Select(r => r.Region)
            .Where(region => !string.IsNullOrWhiteSpace(region))
            .Select(region => region!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(region => region)
            .ToList();

        // The filters are applied here, to the same rows every figure below is derived from.
        // Previously the project half of the sales summary ignored them entirely, so a view
        // narrowed to one office still reported the whole company's backlog beside it.
        var filtered = rows.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(filter?.OfficeCode))
        {
            var normalized = filter.OfficeCode.Trim();
            filtered = filtered.Where(r => string.Equals(r.OfficeCode?.Trim(), normalized, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(filter?.Region))
        {
            var normalized = filter.Region.Trim();
            filtered = filtered.Where(r => string.Equals(r.Region?.Trim(), normalized, StringComparison.OrdinalIgnoreCase));
        }

        var filteredRows = filtered.ToList();
        var open = filteredRows.Where(r => !r.IsClosed).ToList();

        return new BacklogAnalyticsDto
        {
            AsOfUtc = DateTime.UtcNow,
            Summary = new BacklogSummaryDto
            {
                TotalProjectCount = filteredRows.Count,
                OpenProjectCount = open.Count,
                ClosedProjectCount = filteredRows.Count - open.Count,
                OpenBacklogValue = open.Sum(r => r.BudgetRevenue ?? 0m),
                OpenProjectsWithoutBudgetCount = open.Count(r => !r.BudgetRevenue.HasValue),
                BacklogFromOffersValue = open.Where(r => r.OriginalOfferId.HasValue).Sum(r => r.BudgetRevenue ?? 0m),
                BacklogWithoutOfferValue = open.Where(r => !r.OriginalOfferId.HasValue).Sum(r => r.BudgetRevenue ?? 0m),
            },
            ByOffice = BuildBreakdown(open, r => r.OfficeCode),
            ByRegion = BuildBreakdown(open, r => r.Region),
            AvailableOfficeCodes = availableOfficeCodes,
            AvailableRegions = availableRegions,
        };
    }

    private static IReadOnlyList<BacklogBreakdownItemDto> BuildBreakdown(
        List<BacklogRow> open,
        Func<BacklogRow, string?> keySelector) =>
        open
            .Select(row => new { Key = keySelector(row)?.Trim(), row })
            // "Ikke angivet" rather than dropped: a project with no office still holds real
            // backlog, and silently omitting it would make the breakdown disagree with the total.
            .GroupBy(x => string.IsNullOrWhiteSpace(x.Key) ? "Ikke angivet" : x.Key!, StringComparer.OrdinalIgnoreCase)
            .Select(g => new BacklogBreakdownItemDto
            {
                Key = g.Key,
                OpenProjectCount = g.Count(),
                OpenBacklogValue = g.Sum(x => x.row.BudgetRevenue ?? 0m),
            })
            .OrderByDescending(x => x.OpenBacklogValue)
            .ToList();

    private sealed class BacklogRow
    {
        public int ProjectNumber { get; init; }
        public bool IsClosed { get; init; }
        public decimal? BudgetRevenue { get; init; }
        public string? OfficeCode { get; init; }
        public string? Region { get; init; }
        public int? OriginalOfferId { get; init; }
    }
}
