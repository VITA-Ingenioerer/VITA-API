using System.Text.Json.Serialization;

namespace Vita.Planning.Application.DTOs;

public sealed class SourceUserDto
{
    public int EmployeeId { get; set; }
    public string UserPrincipalName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? OfficeLocation { get; set; }
    public string? Department { get; set; }
    public string? EmployeeType { get; set; }
    public int? ManagerEmployeeId { get; set; }
    public string? ManagerId { get; set; }
    public string? ManagerUserPrincipalName { get; set; }
    public string? ManagerName { get; set; }

    public bool? IsActive { get; set; }

    [JsonPropertyName("manager")]
    public string? Manager { get; set; }

}