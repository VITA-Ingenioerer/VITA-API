namespace Vita.Planning.Application.DTOs;

public sealed class EconomicTimeEntryDto
{
    public int Number { get; set; }
    public int ProjectNumber { get; set; }
    public int ActivityNumber { get; set; }
    public int EmployeeNumber { get; set; }
    public DateTime Date { get; set; }
    public string? Text { get; set; }
    public double? NumberOfHours { get; set; }
    public bool? IsApproved { get; set; }
    public bool? IsReconciled { get; set; }
    public DateTime? LastUpdated { get; set; }
    public string? ObjectVersion { get; set; }
}
