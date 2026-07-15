namespace Vita.Planning.Application.DTOs;

public sealed class EmployeeCapacityProfileDto
{
    public int EmployeeCapacityProfileId { get; set; }
    public int EmployeeId { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
    public decimal DefaultWeeklyHours { get; set; }
    public bool IsActive { get; set; }
    public decimal? MondayHours { get; set; }
    public decimal? TuesdayHours { get; set; }
    public decimal? WednesdayHours { get; set; }
    public decimal? ThursdayHours { get; set; }
    public decimal? FridayHours { get; set; }
    public decimal? SaturdayHours { get; set; }
    public decimal? SundayHours { get; set; }
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
}
