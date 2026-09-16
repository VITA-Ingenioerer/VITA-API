using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Vita.Atlas.Infrastructure.Data.Entities;

[Table("offer_disciplines", Schema = "core")]
public sealed class OfferDiscipline
{
    [Key]
    [Column("offer_discipline_id")]
    public int OfferDisciplineId { get; set; }

    [Column("offer_id")]
    public int OfferId { get; set; }

    [Column("engineering_discipline_id")]
    public int EngineeringDisciplineId { get; set; }

    public Offer? Offer { get; set; }
    public EngineeringDiscipline? Discipline { get; set; }
}

[Table("project_metadata_disciplines", Schema = "core")]
public sealed class ProjectMetadataDiscipline
{
    [Key]
    [Column("project_metadata_discipline_id")]
    public int ProjectMetadataDisciplineId { get; set; }

    [Column("project_metadata_id")]
    public int ProjectMetadataId { get; set; }

    [Column("engineering_discipline_id")]
    public int EngineeringDisciplineId { get; set; }

    public ProjectMetadata? ProjectMetadata { get; set; }
    public EngineeringDiscipline? Discipline { get; set; }
}
