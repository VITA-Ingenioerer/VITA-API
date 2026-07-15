using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Vita.Planning.Infrastructure.Data.Entities;

[Table("resource_plans", Schema = "core")]
public sealed class ResourcePlan
{
    [Key]
    [Column("resource_plan_id")]
    public int ResourcePlanId { get; set; }

    [Column("employee_id")]
    public int? EmployeeId { get; set; }

    [Column("virtual_resource_id")]
    public int? VirtualResourceId { get; set; }

    [Column("scenario_id")]
    public int ScenarioId { get; set; }

    [Column("start_year")]
    public int StartYear { get; set; }

    [Column("start_month")]
    public int StartMonth { get; set; }

    [Column("visible_months")]
    public int VisibleMonths { get; set; }

    [MaxLength(1000)]
    [Column("notes")]
    public string? Notes { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; }

    [MaxLength(200)]
    [Column("created_by")]
    public string? CreatedBy { get; set; }

    [MaxLength(200)]
    [Column("updated_by")]
    public string? UpdatedBy { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }
}