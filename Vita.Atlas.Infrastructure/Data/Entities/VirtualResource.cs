using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Vita.Atlas.Infrastructure.Data.Entities;

[Table("virtual_resources", Schema = "core")]
public sealed class VirtualResource
{
    [Key]
    [Column("virtual_resource_id")]
    public int VirtualResourceId { get; set; }

    [Required]
    [MaxLength(50)]
    [Column("code")]
    public string Code { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Column("discipline_id")]
    public int? DisciplineId { get; set; }

    /// <summary>
    /// The partner company, when this resource is a named external (Lars J at PLH arkitekter).
    /// Null means an unfilled role such as NN-BIM. The two kinds are told apart by this column
    /// alone, so there is no flag that can contradict it.
    /// </summary>
    [Column("customer_id")]
    public int? CustomerId { get; set; }

    public Customer? Customer { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; } = true;
}
