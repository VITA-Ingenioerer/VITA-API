using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Vita.Planning.Infrastructure.Data.Entities;

[Table("projects", Schema = "ext")]
public sealed class ExtProject
{
    [Key]
    [Column("project_number")]
    public int ProjectNumber { get; set; }

    [Column("project_name")]
    public string ProjectName { get; set; } = string.Empty;

    [Column("project_group_number")]
    public int ProjectGroupNumber { get; set; }

    [Column("main_project_number")]
    public int? MainProjectNumber { get; set; }

    [Column("customer_number")]
    public int? CustomerNumber { get; set; }

    [Column("contact_person_id")]
    public int? ContactPersonId { get; set; }

    [Column("responsible_employee_number")]
    public int? ResponsibleEmployeeNumber { get; set; }

    [Column("other_responsible_employee_number")]
    public int? OtherResponsibleEmployeeNumber { get; set; }

    [Column("department_number")]
    public int? DepartmentNumber { get; set; }

    [Column("status_number")]
    public int? StatusNumber { get; set; }

    [Column("description")]
    public string? Description { get; set; }

    [Column("other_reference")]
    public string? OtherReference { get; set; }

    [Column("closed_date")]
    public DateTime? ClosedDate { get; set; }

    [Column("delivery_date")]
    public DateTime? DeliveryDate { get; set; }

    [Column("last_updated")]
    public DateTime? LastUpdated { get; set; }

    [Column("is_barred")]
    public bool IsBarred { get; set; }

    [Column("is_closed")]
    public bool IsClosed { get; set; }

    [Column("is_main_project")]
    public bool IsMainProject { get; set; }

    [Column("is_mileage_invoiced")]
    public bool IsMileageInvoiced { get; set; }

    [Column("mileage")]
    public decimal? Mileage { get; set; }

    [Column("cost_price")]
    public decimal? CostPrice { get; set; }

    [Column("sales_price")]
    public decimal? SalesPrice { get; set; }

    [Column("fixed_price")]
    public decimal? FixedPrice { get; set; }

    [Column("invoiced_total")]
    public decimal? InvoicedTotal { get; set; }

    [Column("object_version")]
    public string? ObjectVersion { get; set; }

    [Column("source_last_synced_at")]
    public DateTime SourceLastSyncedAt { get; set; }
}