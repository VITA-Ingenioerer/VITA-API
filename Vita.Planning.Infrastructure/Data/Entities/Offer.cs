using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Vita.Planning.Infrastructure.Data.Entities;

[Table("offers", Schema = "core")]
public sealed class Offer
{
    [Key]
    [Column("offer_id")]
    public int OfferId { get; set; }

    [Required]
    [MaxLength(20)]
    [Column("offer_number")]
    public string OfferNumber { get; set; } = string.Empty;

    [Required]
    [MaxLength(255)]
    [Column("title")]
    public string Title { get; set; } = string.Empty;

    [MaxLength(20)]
    [Column("responsible_initials")]
    public string? ResponsibleInitials { get; set; }

    [MaxLength(20)]
    [Column("responsible_office_code")]
    public string? ResponsibleOfficeCode { get; set; }

    [MaxLength(100)]
    [Column("project_type")]
    public string? ProjectType { get; set; }

    [Column("fee_amount", TypeName = "decimal(18,2)")]
    public decimal? FeeAmount { get; set; }

    [Column("expected_start_year")]
    public int? ExpectedStartYear { get; set; }

    [Column("expected_start_quarter")]
    public int? ExpectedStartQuarter { get; set; }

    [Column("expected_end_year")]
    public int? ExpectedEndYear { get; set; }

    [Column("expected_end_quarter")]
    public int? ExpectedEndQuarter { get; set; }

    [Column("estimated_total_hours", TypeName = "decimal(18,2)")]
    public decimal? EstimatedTotalHours { get; set; }

    [Column("weighted_hours_override", TypeName = "decimal(18,2)")]
    public decimal? WeightedHoursOverride { get; set; }

    [MaxLength(1000)]
    [Column("notes")]
    public string? Notes { get; set; }

    [MaxLength(255)]
    [Column("size_description")]
    public string? SizeDescription { get; set; }

    [Column("add_to_pq_competition")]
    public bool AddToPqCompetition { get; set; }

    [Column("estimated_competition_start_date")]
    public DateOnly? EstimatedCompetitionStartDate { get; set; }

    [Column("pq_submission_date")]
    public DateOnly? PqSubmissionDate { get; set; }

    [Column("delivered_to_pq")]
    public bool? DeliveredToPq { get; set; }

    [Column("has_relation")]
    public bool HasRelation { get; set; }

    [Column("converted_to_project_number")]
    public int? ConvertedToProjectNumber { get; set; }

    [Column("converted_at_utc")]
    public DateTime? ConvertedAtUtc { get; set; }

    [Column("offer_status_id")]
    public int? OfferStatusId { get; set; }

    [Column("customer_id")]
    public int? CustomerId { get; set; }

    [Column("company_contact_id")]
    public int? CustomerContactId { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; }

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

    // Planning metadata — owned by the offer, copied to project_metadata on conversion

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

    // Offer case SharePoint / Outlook folder links — managed by dedicated endpoints

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

    // Project archive — populated at conversion time from workspace provisioning result

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
