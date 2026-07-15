using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Vita.Planning.Infrastructure.Data.Entities;

[Table("customers", Schema = "core")]
public sealed class Customer
{
    [Key]
    [Column("customer_id")]
    public int CustomerId { get; set; }

    [Column("ext_customer_number")]
    public int? ExtCustomerNumber { get; set; }

    [Required]
    [MaxLength(255)]
    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(30)]
    [Column("customer_status")]
    public string CustomerStatus { get; set; } = "Active";

    [Required]
    [MaxLength(30)]
    [Column("customer_source")]
    public string CustomerSource { get; set; } = "Local";

    [MaxLength(255)]
    [Column("object_version")]
    public string? ObjectVersion { get; set; }

    [Column("source_last_synced_at")]
    public DateTime? SourceLastSyncedAt { get; set; }

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

    [MaxLength(20)]
    [Column("cvr_number")]
    public string? CvrNumber { get; set; }

    [MaxLength(255)]
    [Column("address_line")]
    public string? AddressLine { get; set; }

    [MaxLength(20)]
    [Column("postal_code")]
    public string? PostalCode { get; set; }

    [MaxLength(100)]
    [Column("city")]
    public string? City { get; set; }

    [MaxLength(10)]
    [Column("country_code")]
    public string? CountryCode { get; set; }

    [MaxLength(50)]
    [Column("cvr_status")]
    public string? CvrStatus { get; set; }

    [MaxLength(50)]
    [Column("industry_code")]
    public string? IndustryCode { get; set; }

    [MaxLength(255)]
    [Column("industry_name")]
    public string? IndustryName { get; set; }

    [MaxLength(100)]
    [Column("source_reference")]
    public string? SourceReference { get; set; }
}
