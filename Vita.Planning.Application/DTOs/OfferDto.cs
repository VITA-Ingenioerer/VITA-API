namespace Vita.Planning.Application.DTOs;

public sealed class OfferDto
{
    public int OfferId { get; set; }
    public string OfferNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? ResponsibleInitials { get; set; }
    public string? ResponsibleOfficeCode { get; set; }
    public string? ProjectType { get; set; }
    public decimal? FeeAmount { get; set; }
    public int? ExpectedStartYear { get; set; }
    public int? ExpectedStartQuarter { get; set; }
    public int? ExpectedEndYear { get; set; }
    public int? ExpectedEndQuarter { get; set; }
    public decimal? EstimatedTotalHours { get; set; }
    public decimal? WeightedHoursOverride { get; set; }
    public decimal? CalculatedWeightedHours { get; set; }
    public string? Notes { get; set; }
    public string? SizeDescription { get; set; }
    public bool AddToPqCompetition { get; set; }
    public DateOnly? EstimatedCompetitionStartDate { get; set; }
    public DateOnly? PqSubmissionDate { get; set; }
    public bool? DeliveredToPq { get; set; }
    public bool HasRelation { get; set; }
    public int? ConvertedToProjectNumber { get; set; }
    public DateTime? ConvertedAtUtc { get; set; }
    public int? OfferStatusId { get; set; }
    public string? OfferStatusCode { get; set; }
    public string? OfferStatusName { get; set; }
    public int? CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerCvrNumber { get; set; }
    public int? CustomerContactId { get; set; }
    public IReadOnlyList<OfferPartnerDto> Partners { get; set; } = [];
    public IReadOnlyList<int> SegmentIds { get; set; } = [];
    public IReadOnlyList<string> Segments { get; set; } = [];
    public bool IsActive { get; set; }
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }

    // Planning metadata — owned by the offer
    public string? PlanningCategory { get; set; }
    public string? PlanningStatus { get; set; }
    public string? DisciplineOwner { get; set; }
    public string? DefaultDescription { get; set; }
    public string? ColorTag { get; set; }
    public string? PlanningGroup { get; set; }
    public string? Phase { get; set; }
    public decimal? ProbabilityPercent { get; set; }
    public string? LastPlanningReviewBy { get; set; }
    public int? Priority { get; set; }
    public bool IsBillableForPlanning { get; set; }
    public bool IsAbsence { get; set; }
    public bool IsInternal { get; set; }
    public bool IsProbableCase { get; set; }
    public bool IsVisibleInPlanner { get; set; }
    public bool DailyPlanningEnabled { get; set; }
    public decimal? EntrepriseSum { get; set; }
    public string? EntrepriseForm { get; set; }
    public decimal? ArealM2 { get; set; }
    public string? Raadgivningsform { get; set; }
    public string? Rolle { get; set; }
    public string? ByghherreKontaktperson { get; set; }

    // Lookup IDs (stored) + display names (resolved)
    public int? CompetitionFormId { get; set; }
    public string? CompetitionFormName { get; set; }
    public int? EnterpriseFormId { get; set; }
    public string? EnterpriseFormName { get; set; }
    public int? ConsultantFormId { get; set; }
    public string? ConsultantFormName { get; set; }
    public int? ProjectTypeId { get; set; }
    public string? ProjectTypeName { get; set; }
    public int? ProjectRoleId { get; set; }
    public string? ProjectRoleName { get; set; }
    public int? ComplexityLevelId { get; set; }
    public string? ComplexityLevelName { get; set; }
    public int? EngineeringDisciplineId { get; set; }
    public string? EngineeringDisciplineName { get; set; }

    // Offer case SharePoint / Outlook folder links
    public string? OfferCaseUrl { get; set; }
    public string? OfferCasePath { get; set; }
    public string? OfferCaseDriveId { get; set; }
    public string? OfferCaseFolderItemId { get; set; }
    public string? OfferCaseOutlookFolderId { get; set; }

    // Project archive — populated at conversion time
    public string? ProjectArchiveUrl { get; set; }
    public string? ProjectArchiveSiteId { get; set; }
    public string? ProjectArchiveDriveId { get; set; }
    public string? ProjectArchiveOutlookFolderId { get; set; }

    // Project address — from DAWA lookup
    public string? ProjectDawaId { get; set; }
    public string? ProjectStreetAddress { get; set; }
    public string? ProjectPostalCode { get; set; }
    public string? ProjectCity { get; set; }
    public string? ProjectMunicipality { get; set; }
    public string? ProjectRegion { get; set; }
}
