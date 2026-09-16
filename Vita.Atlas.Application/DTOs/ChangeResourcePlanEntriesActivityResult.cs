namespace Vita.Atlas.Application.DTOs;

public sealed class ChangeResourcePlanEntriesActivityResult
{
    /// <summary>Entries re-pointed in place, because the target activity had nothing on that date.</summary>
    public int MovedCount { get; set; }

    /// <summary>
    /// Entries folded into an existing entry on the target activity. One entry per
    /// plan/target/date/activity is all the schema allows, so a collision has to become a
    /// single row with the hours summed.
    /// </summary>
    public int MergedCount { get; set; }

    /// <summary>Total hours carried over from the old activity — moved and merged alike.</summary>
    public decimal MovedHours { get; set; }

    /// <summary>The line as it stands afterwards: every entry now on the target activity.</summary>
    public IReadOnlyList<ResourcePlanEntryDto> Entries { get; set; } = [];
}
