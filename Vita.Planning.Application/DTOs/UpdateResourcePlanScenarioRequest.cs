using System.ComponentModel.DataAnnotations;

namespace Vita.Planning.Application.DTOs;

public sealed class UpdateResourcePlanScenarioRequest
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(510)]
    public string? Description { get; set; }

    public bool IsDefault { get; set; }
    public bool IsLocked { get; set; }

    [MaxLength(200)]
    public string? UpdatedBy { get; set; }
}
