namespace Vita.Planning.Application.DTOs;

public sealed class LookupItemDto
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}

public sealed class SegmentDto
{
    public int SegmentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}

public sealed class VirtualResourceDto
{
    public int VirtualResourceId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int? DisciplineId { get; set; }
}
