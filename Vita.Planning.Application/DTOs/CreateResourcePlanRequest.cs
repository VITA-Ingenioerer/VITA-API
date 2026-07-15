using System.ComponentModel.DataAnnotations;

namespace Vita.Planning.Application.DTOs;

public sealed class CreateResourcePlanRequest
{
    [Required]
    public int EmployeeId { get; set; }

    [Required]
    public int ScenarioId { get; set; }

    [Required]
    public int StartYear { get; set; }

    [Required]
    public int StartMonth { get; set; }

    [Required]
    public int VisibleMonths { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    public bool IsActive { get; set; }

    [MaxLength(200)]
    public string? CreatedBy { get; set; }
}