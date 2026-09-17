namespace Vita.Atlas.Application.DTOs;

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

/// <summary>
/// A plannable resource that is not an employee: either an unfilled role (NN-BIM) or a named
/// person at a partner company (Lars J at PLH arkitekter). Which one is decided by
/// <see cref="CustomerId"/>, never by a separate flag.
/// </summary>
public sealed class VirtualResourceDto
{
    public int VirtualResourceId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int? DisciplineId { get; set; }
    public int? CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public bool IsActive { get; set; }

    /// <summary>"Placeholder" or "External" — derived, so the planner does not re-implement the rule.</summary>
    public string Kind => CustomerId.HasValue ? "External" : "Placeholder";
}

public sealed class UpsertVirtualResourceRequest
{
    /// <summary>Matched against Timer-tabel initials on import, so it must be unique. e.g. "NN-BIM".</summary>
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public int? DisciplineId { get; set; }

    /// <summary>Set for a named external, left null for an unfilled role.</summary>
    public int? CustomerId { get; set; }

    public bool IsActive { get; set; } = true;
}
