using System.ComponentModel.DataAnnotations;

namespace Vita.Planning.Application.DTOs;

public sealed class ApproveTimeEntriesRequest
{
    [Required]
    [MinLength(1)]
    public IReadOnlyList<int> Numbers { get; set; } = Array.Empty<int>();

    public int? BookOn { get; set; }
}
