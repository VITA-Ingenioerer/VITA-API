using System.ComponentModel.DataAnnotations;

namespace Vita.Planning.Application.DTOs;

public sealed class CreateEmployeeCapacityOverrideRequest
{
    [Required]
    public int EmployeeId { get; set; }

    [Required]
    public DateOnly EffectiveFrom { get; set; }

    public DateOnly? EffectiveTo { get; set; }

    [Required]
    [MaxLength(60)]
    public string OverrideType { get; set; } = string.Empty;

    public decimal? WeeklyHours { get; set; }
    public decimal? CapacityFactor { get; set; }
    public decimal? FixedHoursPerWeek { get; set; }

    [MaxLength(500)]
    public string? Reason { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }

    public bool IsActive { get; set; }

    [Range(0, 24)]
    public decimal? MondayHours { get; set; }

    [Range(0, 24)]
    public decimal? TuesdayHours { get; set; }

    [Range(0, 24)]
    public decimal? WednesdayHours { get; set; }

    [Range(0, 24)]
    public decimal? ThursdayHours { get; set; }

    [Range(0, 24)]
    public decimal? FridayHours { get; set; }

    [Range(0, 24)]
    public decimal? SaturdayHours { get; set; }

    [Range(0, 24)]
    public decimal? SundayHours { get; set; }

    [MaxLength(200)]
    public string? CreatedBy { get; set; }
}
