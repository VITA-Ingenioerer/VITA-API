using System.ComponentModel.DataAnnotations;

namespace Vita.Planning.Application.DTOs;

public sealed class CreateOutOfOfficeCalendarEventRequest
{
    [Required]
    [MaxLength(255)]
    public string Title { get; set; } = string.Empty;

    [Required]
    public DateOnly StartDate { get; set; }

    [Required]
    public DateOnly EndDate { get; set; }
}

public sealed class OutOfOfficeCalendarEventDto
{
    public string GraphEventId { get; set; } = string.Empty;
    public int EmployeeId { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
