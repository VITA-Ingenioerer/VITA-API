using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Vita.Planning.Infrastructure.Data.Entities;

[Table("segments", Schema = "core")]
public sealed class Segment
{
    [Key]
    [Column("segment_id")]
    public int SegmentId { get; set; }

    [Required]
    [MaxLength(100)]
    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Column("sort_order")]
    public int SortOrder { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; } = true;
}

[Table("offer_segments", Schema = "core")]
public sealed class OfferSegment
{
    [Column("offer_id")]
    public int OfferId { get; set; }

    [Column("segment_id")]
    public int SegmentId { get; set; }

    public Offer? Offer { get; set; }
    public Segment? SegmentEntity { get; set; }
}

[Table("project_metadata_segments", Schema = "core")]
public sealed class ProjectMetadataSegment
{
    [Column("project_metadata_id")]
    public int ProjectMetadataId { get; set; }

    [Column("segment_id")]
    public int SegmentId { get; set; }

    public ProjectMetadata? ProjectMetadata { get; set; }
    public Segment? SegmentEntity { get; set; }
}
