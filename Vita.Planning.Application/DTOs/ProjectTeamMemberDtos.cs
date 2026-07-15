using System.ComponentModel.DataAnnotations;

namespace Vita.Planning.Application.DTOs;

public sealed class ProjectTeamMemberDto
{
    public int ProjectTeamMemberId { get; set; }
    public string MemberType { get; set; } = string.Empty;

    // Internal member fields
    public int? EmployeeId { get; set; }
    public string? DisplayName { get; set; }
    public string? OfficeLocation { get; set; }
    public string? Department { get; set; }

    // External member fields
    public int? CompanyContactId { get; set; }
    public string? ContactName { get; set; }
    public string? CompanyName { get; set; }
    public string? Email { get; set; }

    public string? RoleDescription { get; set; }
    public int? EngineeringDisciplineId { get; set; }
    public string? EngineeringDisciplineName { get; set; }
    public int SortOrder { get; set; }
}

public sealed class AddProjectTeamMemberRequest
{
    [Required]
    [MaxLength(20)]
    public string MemberType { get; set; } = string.Empty;

    public int? EmployeeId { get; set; }
    public int? CompanyContactId { get; set; }

    [MaxLength(255)]
    public string? RoleDescription { get; set; }

    public int? EngineeringDisciplineId { get; set; }
    public int SortOrder { get; set; }
}

public sealed class UpdateProjectTeamMemberRequest
{
    [MaxLength(255)]
    public string? RoleDescription { get; set; }

    public int? EngineeringDisciplineId { get; set; }
    public int SortOrder { get; set; }
}
