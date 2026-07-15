using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Vita.Planning.Infrastructure.Data.Entities;

[Table("activities", Schema = "ext")]
public sealed class ExtActivity
{
    [Key]
    [Column("activity_number")]
    public int ActivityNumber { get; set; }

    [Column("activity_group_number")]
    public int ActivityGroupNumber { get; set; }

    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Column("cost_price_markup_percentage")]
    public decimal? CostPriceMarkupPercentage { get; set; }

    [Column("cutoff_date")]
    public DateTime? CutoffDate { get; set; }

    [Column("hide_in_search")]
    public bool? HideInSearch { get; set; }

    [Column("in_lieu_code")]
    public int InLieuCode { get; set; }

    [Column("is_accessible")]
    public bool? IsAccessible { get; set; }

    [Column("sales_price_after")]
    public decimal? SalesPriceAfter { get; set; }

    [Column("sales_price_before")]
    public decimal? SalesPriceBefore { get; set; }

    [Column("object_version")]
    public string? ObjectVersion { get; set; }

    [Column("source_last_synced_at")]
    public DateTime SourceLastSyncedAt { get; set; }
}