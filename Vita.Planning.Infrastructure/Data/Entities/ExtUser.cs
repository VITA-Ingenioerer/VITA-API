using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Vita.Planning.Infrastructure.Data.Entities;

[Table("users", Schema = "ext")]
public sealed class ExtUser
{
    [Key]
    [Column("employee_id")]
    public int EmployeeId { get; set; }

    [Column("user_principal_name")]
    public string UserPrincipalName { get; set; } = string.Empty;

    [Column("display_name")]
    public string DisplayName { get; set; } = string.Empty;

    [Column("office_location")]
    public string? OfficeLocation { get; set; }

    [Column("department")]
    public string? Department { get; set; }

    [Column("employee_type")]
    public string? EmployeeType { get; set; }

    [Column("manager_employee_id")]
    public int? ManagerEmployeeId { get; set; }

    [Column("source_last_synced_at")]
    public DateTime SourceLastSyncedAt { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; }

    // Employee classification. Microsoft Entra custom security attributes
    // (VITA.PrimaryFaglighed / VITA.SecondaryFagligheder / VITA.Profession) are the
    // authority for all three; these are a local read model so the planner can filter and
    // group the roster without a Graph call per employee. Written only by
    // EmployeeIdentityService, after Entra has accepted the change.

    [Column("primary_faglighed")]
    public string? PrimaryFaglighed { get; set; }

    [Column("profession")]
    public string? Profession { get; set; }

    public ICollection<UserSecondaryFaglighed> SecondaryFagligheder { get; set; } =
        new List<UserSecondaryFaglighed>();
}