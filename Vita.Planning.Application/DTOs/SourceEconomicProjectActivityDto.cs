namespace Vita.Planning.Application.DTOs;

public sealed class SourceEconomicProjectActivityDto
{
    public int Number { get; set; }
    public int ProjectNumber { get; set; }
    public int ActivityNumber { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int? ResponsibleEmployeeNumber { get; set; }
    public bool? Completed { get; set; }
    public string? ObjectVersion { get; set; }
    public DateTime? LastUpdated { get; set; }
}
