using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Vita.Planning.Infrastructure.Data.Entities;

[Table("project_metadata", Schema = "core")]
public sealed class ProjectMetadata
{
    [Key]
    [Column("project_metadata_id")]
    public int ProjectMetadataId { get; set; }

    [Required]
    [Column("project_number")]
    public int ProjectNumber { get; set; }

    [Column("original_offer_id")]
    public int? OriginalOfferId { get; set; }

    [MaxLength(20)]
    [Column("original_offer_number")]
    public string? OriginalOfferNumber { get; set; }

    [MaxLength(100)]
    [Column("planning_category")]
    public string? PlanningCategory { get; set; }

    [MaxLength(100)]
    [Column("planning_status")]
    public string? PlanningStatus { get; set; }

    [MaxLength(100)]
    [Column("discipline_owner")]
    public string? DisciplineOwner { get; set; }

    [Column("default_description", TypeName = "nvarchar(max)")]
    public string? DefaultDescription { get; set; }

    [MaxLength(50)]
    [Column("color_tag")]
    public string? ColorTag { get; set; }

    [MaxLength(100)]
    [Column("planning_group")]
    public string? PlanningGroup { get; set; }

    [MaxLength(100)]
    [Column("phase")]
    public string? Phase { get; set; }

    [Column("probability_percent", TypeName = "decimal(5,2)")]
    public decimal? ProbabilityPercent { get; set; }

    [Column("budget_hours", TypeName = "decimal(18,2)")]
    public decimal? BudgetHours { get; set; }

    [Column("budget_revenue", TypeName = "decimal(18,2)")]
    public decimal? BudgetRevenue { get; set; }

    [MaxLength(255)]
    [Column("last_planning_review_by")]
    public string? LastPlanningReviewBy { get; set; }

    [Column("priority")]
    public int? Priority { get; set; }

    [Column("is_billable_for_planning")]
    public bool IsBillableForPlanning { get; set; }

    [Column("is_absence")]
    public bool IsAbsence { get; set; }

    [Column("is_internal")]
    public bool IsInternal { get; set; }

    [Column("is_probable_case")]
    public bool IsProbableCase { get; set; }

    [Column("is_visible_in_planner")]
    public bool IsVisibleInPlanner { get; set; }

    [Column("daily_planning_enabled")]
    public bool DailyPlanningEnabled { get; set; }

    [Column("start_date")]
    public DateOnly? StartDate { get; set; }

    [Column("end_date")]
    public DateOnly? EndDate { get; set; }

    [MaxLength(1000)]
    [Column("notes")]
    public string? Notes { get; set; }

    [MaxLength(255)]
    [Column("size_description")]
    public string? SizeDescription { get; set; }

    [MaxLength(20)]
    [Column("responsible_initials")]
    public string? ResponsibleInitials { get; set; }

    [MaxLength(20)]
    [Column("responsible_office_code")]
    public string? ResponsibleOfficeCode { get; set; }

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

    [Column("entreprise_sum", TypeName = "decimal(18,2)")]
    public decimal? EntrepriseSum { get; set; }

    [MaxLength(255)]
    [Column("entreprise_form")]
    public string? EntrepriseForm { get; set; }

    [Column("areal_m2", TypeName = "decimal(10,2)")]
    public decimal? ArealM2 { get; set; }

    [MaxLength(255)]
    [Column("raadgivningsform")]
    public string? Raadgivningsform { get; set; }

    [MaxLength(255)]
    [Column("rolle")]
    public string? Rolle { get; set; }

    [MaxLength(255)]
    [Column("bygherre_kontaktperson")]
    public string? ByghherreKontaktperson { get; set; }

    [Column("competition_form_id")]
    public int? CompetitionFormId { get; set; }

    [Column("enterprise_form_id")]
    public int? EnterpriseFormId { get; set; }

    [Column("consultant_form_id")]
    public int? ConsultantFormId { get; set; }

    [Column("project_type_id")]
    public int? ProjectTypeId { get; set; }

    [Column("project_role_id")]
    public int? ProjectRoleId { get; set; }

    [Column("complexity_level_id")]
    public int? ComplexityLevelId { get; set; }

    [Column("engineering_discipline_id")]
    public int? EngineeringDisciplineId { get; set; }

    [MaxLength(255)]
    [Column("project_archive_group_id")]
    public string? ProjectArchiveGroupId { get; set; }

    [Column("project_archive_url", TypeName = "nvarchar(max)")]
    public string? ProjectArchiveUrl { get; set; }

    [MaxLength(255)]
    [Column("project_archive_site_id")]
    public string? ProjectArchiveSiteId { get; set; }

    [MaxLength(255)]
    [Column("project_archive_drive_id")]
    public string? ProjectArchiveDriveId { get; set; }

    [Column("project_archive_outlook_folder_id", TypeName = "nvarchar(max)")]
    public string? ProjectArchiveOutlookFolderId { get; set; }

    // Offer case — copied from the original offer at conversion time

    [Column("offer_case_url", TypeName = "nvarchar(max)")]
    public string? OfferCaseUrl { get; set; }

    [MaxLength(500)]
    [Column("offer_case_path")]
    public string? OfferCasePath { get; set; }

    [MaxLength(255)]
    [Column("offer_case_drive_id")]
    public string? OfferCaseDriveId { get; set; }

    [MaxLength(255)]
    [Column("offer_case_folder_item_id")]
    public string? OfferCaseFolderItemId { get; set; }

    [Column("offer_case_outlook_folder_id", TypeName = "nvarchar(max)")]
    public string? OfferCaseOutlookFolderId { get; set; }

    // Project address — from DAWA lookup
    [MaxLength(50)]
    [Column("project_dawa_id")]
    public string? ProjectDawaId { get; set; }

    [MaxLength(255)]
    [Column("project_street_address")]
    public string? ProjectStreetAddress { get; set; }

    [MaxLength(10)]
    [Column("project_postal_code")]
    public string? ProjectPostalCode { get; set; }

    [MaxLength(100)]
    [Column("project_city")]
    public string? ProjectCity { get; set; }

    [MaxLength(100)]
    [Column("project_municipality")]
    public string? ProjectMunicipality { get; set; }

    [MaxLength(100)]
    [Column("project_region")]
    public string? ProjectRegion { get; set; }
}
