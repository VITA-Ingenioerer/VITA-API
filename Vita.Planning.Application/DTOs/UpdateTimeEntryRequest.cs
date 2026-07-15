using System.ComponentModel.DataAnnotations;

namespace Vita.Planning.Application.DTOs;

public sealed class UpdateTimeEntryRequest
{
    [Required]
    public int Number { get; set; }

    [Required]
    public int ProjectNumber { get; set; }

    [Required]
    public int ActivityNumber { get; set; }

    [Required]
    public int EmployeeNumber { get; set; }

    [Required]
    public DateTime Date { get; set; }

    public double? NumberOfHours { get; set; }
    public string? Text { get; set; }
    public string? ObjectVersion { get; set; }
}
