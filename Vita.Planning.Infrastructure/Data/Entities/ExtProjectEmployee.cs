using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Vita.Planning.Infrastructure.Data.Entities;

[Table("project_employees", Schema = "ext")]
public sealed class ExtProjectEmployee
{
    [Key]
    [Column("employee_number")]
    public int EmployeeNumber { get; set; }

    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Column("group_number")]
    public int? GroupNumber { get; set; }

    [Column("can_approve")]
    public bool CanApprove { get; set; }

    [Column("can_invoice")]
    public bool CanInvoice { get; set; }

    [Column("is_barred")]
    public bool IsBarred { get; set; }

    [Column("address")]
    public string? Address { get; set; }

    [Column("city")]
    public string? City { get; set; }

    [Column("zip_code")]
    public string? ZipCode { get; set; }

    [Column("cost_price_after")]
    public decimal? CostPriceAfter { get; set; }

    [Column("cost_price_before")]
    public decimal? CostPriceBefore { get; set; }

    [Column("sales_price_after")]
    public decimal? SalesPriceAfter { get; set; }

    [Column("sales_price_before")]
    public decimal? SalesPriceBefore { get; set; }

    [Column("cutoff_date")]
    public DateTime? CutoffDate { get; set; }

    [Column("object_version")]
    public string? ObjectVersion { get; set; }

    [Column("source_last_synced_at")]
    public DateTime SourceLastSyncedAt { get; set; }
}