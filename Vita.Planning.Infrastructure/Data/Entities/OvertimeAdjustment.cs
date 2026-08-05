using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Vita.Planning.Infrastructure.Data.Entities;

[Table("overtime_adjustments", Schema = "core")]
public sealed class OvertimeAdjustment
{
    [Key]
    [Column("overtime_adjustment_id")]
    public int OvertimeAdjustmentId { get; set; }

    [Column("employee_id")]
    public int EmployeeId { get; set; }

    [Column("effective_month")]
    public DateOnly EffectiveMonth { get; set; }

    [Column("hours")]
    public decimal Hours { get; set; }

    [MaxLength(2000)]
    [Column("notes")]
    public string? Notes { get; set; }

    [Column("created_by_employee_id")]
    public int? CreatedByEmployeeId { get; set; }

    [Column("created_at_utc")]
    public DateTime CreatedAtUtc { get; set; }
}
