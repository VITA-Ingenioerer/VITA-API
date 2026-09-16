namespace Vita.Atlas.Application.DTOs;

public sealed class ChangeResourcePlanEntriesTargetResult
{
    /// <summary>Entries re-pointed in place, because the destination had nothing on that date and activity.</summary>
    public int MovedCount { get; set; }

    /// <summary>Entries folded into an existing destination entry, whose hours were summed.</summary>
    public int MergedCount { get; set; }

    public decimal MovedHours { get; set; }

    /// <summary>
    /// Entries whose activity has no counterpart in the destination project's own activity
    /// list and therefore landed in the "Ikke-tildelt" bucket. Hours are never dropped —
    /// only the activity is, and only when the destination has nothing to map it to.
    /// </summary>
    public int UnmappedActivityEntryCount { get; set; }

    /// <summary>The activity numbers behind <see cref="UnmappedActivityEntryCount"/>, for the message shown to the planner.</summary>
    public IReadOnlyList<int> UnmappedActivityNumbers { get; set; } = [];

    /// <summary>The line as it stands afterwards, on the destination target.</summary>
    public IReadOnlyList<ResourcePlanEntryDto> Entries { get; set; } = [];
}
