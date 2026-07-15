using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Vita.Planning.Infrastructure.Data.Entities;

[Table("project_team_members", Schema = "core")]
public sealed class ProjectTeamMember
{
    [Key]
    [Column("project_team_member_id")]
    public int ProjectTeamMemberId { get; set; }

    [Column("project_metadata_id")]
    public int ProjectMetadataId { get; set; }

    [Required]
    [MaxLength(20)]
    [Column("member_type")]
    public string MemberType { get; set; } = string.Empty;

    [Column("employee_id")]
    public int? EmployeeId { get; set; }

    [Column("company_contact_id")]
    public int? CompanyContactId { get; set; }

    [MaxLength(255)]
    [Column("role_description")]
    public string? RoleDescription { get; set; }

    [Column("engineering_discipline_id")]
    public int? EngineeringDisciplineId { get; set; }

    [Column("sort_order")]
    public int SortOrder { get; set; }

    [Column("created_at_utc")]
    public DateTime CreatedAtUtc { get; set; }

    [Column("updated_at_utc")]
    public DateTime? UpdatedAtUtc { get; set; }
}
