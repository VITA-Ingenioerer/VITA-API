using System.ComponentModel.DataAnnotations;

namespace Vita.Atlas.Application.DTOs;

/// <summary>
/// The user fields VITA maintains in Entra. Every one of them is written to Entra first and
/// mirrored into ext.users afterwards — Entra stays the system of record.
/// </summary>
public sealed class UpdateUserProfileRequest
{
    [MaxLength(200)]
    public string? Department { get; set; }

    [MaxLength(200)]
    public string? OfficeLocation { get; set; }

    /// <summary>Employee number of the manager, or null to remove the manager entirely.</summary>
    public int? ManagerEmployeeId { get; set; }
}
