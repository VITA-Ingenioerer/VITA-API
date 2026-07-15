namespace Vita.Planning.Application.DTOs;

public sealed class AbsenceRegistrationDto
{
    public int TimeEntryNumber { get; set; }
    public int EmployeeId { get; set; }
    public string? EmployeeDisplayName { get; set; }
    public int ProjectNumber { get; set; }
    public int ActivityNumber { get; set; }
    public DateOnly Date { get; set; }
    public double Hours { get; set; }
    public string? Text { get; set; }
    public bool? IsApproved { get; set; }
}
