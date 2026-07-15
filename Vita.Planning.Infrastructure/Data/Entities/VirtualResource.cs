using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Vita.Planning.Infrastructure.Data.Entities;

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

    [Column("is_active")]
    public bool IsActive { get; set; } = true;
}
