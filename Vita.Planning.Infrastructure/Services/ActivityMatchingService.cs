using Microsoft.EntityFrameworkCore;
using Vita.Planning.Application.Interfaces;
using Vita.Planning.Infrastructure.Data;

namespace Vita.Planning.Infrastructure.Services;

public sealed class ActivityMatchingService : IActivityMatchingService
{
    // Below this, a "match" is more likely coincidence than intent (e.g. one shared word).
    private const double MinimumConfidentScore = 0.6;

    private readonly PlanningDbContext _dbContext;

    public ActivityMatchingService(PlanningDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<int?> MatchActivityAsync(
        int planningTargetId,
        string? description,
        int? employeeId,
        CancellationToken cancellationToken = default)
    {
        var projectNumber = await _dbContext.PlanningTargets
            .AsNoTracking()
            .Where(t => t.PlanningTargetId == planningTargetId)
            .Select(t => t.ExtProjectNumber)
            .FirstOrDefaultAsync(cancellationToken);

        if (!projectNumber.HasValue)
        {
            return null;
        }

        var candidates = await (
            from projectActivity in _dbContext.ProjectActivities.AsNoTracking()
            join activity in _dbContext.Activities.AsNoTracking()
                on projectActivity.ActivityNumber equals activity.ActivityNumber
            where projectActivity.ProjectNumber == projectNumber.Value
            select new ActivityCandidate(projectActivity.Number, activity.Name)
        ).ToListAsync(cancellationToken);

        if (candidates.Count == 0)
        {
            return null;
        }

        var descriptionMatch = TryMatch(candidates, description);

        if (descriptionMatch.HasValue)
        {
            return descriptionMatch;
        }

        if (!employeeId.HasValue)
        {
            return null;
        }

        var department = await _dbContext.Users
            .AsNoTracking()
            .Where(u => u.EmployeeId == employeeId.Value)
            .Select(u => u.Department)
            .FirstOrDefaultAsync(cancellationToken);

        return TryMatch(candidates, department);
    }

    private static int? TryMatch(IReadOnlyList<ActivityCandidate> candidates, string? searchText)
    {
        var normalizedSearch = Normalize(searchText);

        if (normalizedSearch.Length == 0)
        {
            return null;
        }

        var searchTokens = Tokenize(normalizedSearch);

        ActivityCandidate? best = null;
        var bestScore = 0d;

        foreach (var candidate in candidates)
        {
            var normalizedName = Normalize(candidate.Name);

            if (normalizedName.Length == 0)
            {
                continue;
            }

            var score = Score(normalizedSearch, searchTokens, normalizedName);

            if (score > bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }

        return bestScore >= MinimumConfidentScore ? best?.ProjectActivityId : null;
    }

    private static double Score(string normalizedSearch, IReadOnlySet<string> searchTokens, string normalizedName)
    {
        if (normalizedSearch == normalizedName)
        {
            return 1.0;
        }

        if (normalizedSearch.Contains(normalizedName, StringComparison.Ordinal)
            || normalizedName.Contains(normalizedSearch, StringComparison.Ordinal))
        {
            return 0.9;
        }

        var nameTokens = Tokenize(normalizedName);

        if (searchTokens.Count == 0 || nameTokens.Count == 0)
        {
            return 0;
        }

        var intersection = searchTokens.Intersect(nameTokens).Count();
        var union = searchTokens.Union(nameTokens).Count();

        return union == 0 ? 0 : (double)intersection / union;
    }

    private static HashSet<string> Tokenize(string normalized)
    {
        return normalized
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(token => token.Length > 1)
            .ToHashSet();
    }

    // Uppercase (Danish-aware via invariant culture), collapse punctuation to spaces, trim.
    private static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var chars = value.Trim().ToUpperInvariant()
            .Select(c => char.IsLetterOrDigit(c) || char.IsWhiteSpace(c) ? c : ' ')
            .ToArray();

        return string.Join(' ', new string(chars).Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private sealed record ActivityCandidate(int ProjectActivityId, string Name);
}
