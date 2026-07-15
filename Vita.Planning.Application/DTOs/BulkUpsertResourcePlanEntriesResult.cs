namespace Vita.Planning.Application.DTOs;

public sealed class BulkUpsertResourcePlanEntriesResult
{
    public IReadOnlyList<ResourcePlanEntryDto> Entries { get; set; } = [];
}
