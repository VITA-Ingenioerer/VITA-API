using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Vita.Planning.Infrastructure.Data.Entities;

[Table("customer_partner_roles", Schema = "core")]
public sealed class CustomerPartnerRole
{
    [Key]
    [Column("customer_partner_role_id")]
    public int CustomerPartnerRoleId { get; set; }

    [Column("planning_target_id")]
    public int PlanningTargetId { get; set; }

    [Column("customer_id")]
    public int CustomerId { get; set; }

    [Column("planning_partner_role_type_id")]
    public int PlanningPartnerRoleTypeId { get; set; }

    [MaxLength(100)]
    [Column("role_type")]
    public string? RoleType { get; set; }

    [Column("is_primary")]
    public bool IsPrimary { get; set; }

    [Column("company_contact_id")]
    public int? CompanyContactId { get; set; }

    [MaxLength(100)]
    [Column("created_by")]
    public string? CreatedBy { get; set; }

    [MaxLength(100)]
    [Column("updated_by")]
    public string? UpdatedBy { get; set; }

    [Column("created_at_utc")]
    public DateTime CreatedAtUtc { get; set; }

    [Column("updated_at_utc")]
    public DateTime? UpdatedAtUtc { get; set; }

    public PlanningTarget? PlanningTarget { get; set; }
    public Customer? Customer { get; set; }
    public PlanningPartnerRoleType? PlanningPartnerRoleType { get; set; }
    public CompanyContact? CompanyContact { get; set; }
}
