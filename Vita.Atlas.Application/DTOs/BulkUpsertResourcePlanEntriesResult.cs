namespace Vita.Atlas.Application.DTOs;

public sealed class BulkUpsertResourcePlanEntriesResult
{
    public IReadOnlyList<ResourcePlanEntryDto> Entries { get; set; } = [];
}
