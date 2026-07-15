using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Vita.Planning.Infrastructure.Data.Entities;

[Table("project_groups", Schema = "ext")]
public sealed class ExtProjectGroup
{
    [Key]
    [Column("project_group_number")]
    public int ProjectGroupNumber { get; set; }

    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Column("type_number")]
    public int TypeNumber { get; set; }

    [Column("cost_account_closed")]
    public int? CostAccountClosed { get; set; }

    [Column("cost_account_ongoing")]
    public int? CostAccountOngoing { get; set; }

    [Column("cost_account_ongoing_type")]
    public int CostAccountOngoingType { get; set; }

    [Column("cost_contra_account_ongoing")]
    public int? CostContraAccountOngoing { get; set; }

    [Column("sales_account_closed")]
    public int? SalesAccountClosed { get; set; }

    [Column("sales_account_ongoing")]
    public int? SalesAccountOngoing { get; set; }

    [Column("sales_account_ongoing_type")]
    public int SalesAccountOngoingType { get; set; }

    [Column("sales_contra_account_ongoing")]
    public int? SalesContraAccountOngoing { get; set; }

    [Column("include_cost_price_in_finance")]
    public bool IncludeCostPriceInFinance { get; set; }

    [Column("include_sales_price_in_finance")]
    public bool IncludeSalesPriceInFinance { get; set; }

    [Column("object_version")]
    public string? ObjectVersion { get; set; }

    [Column("source_last_synced_at")]
    public DateTime SourceLastSyncedAt { get; set; }
}