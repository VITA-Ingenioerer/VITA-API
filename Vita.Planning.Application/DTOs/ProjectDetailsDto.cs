namespace Vita.Planning.Application.DTOs;

public sealed class ProjectDetailsDto
{
    // From e-conomic (ext.projects)
    public int ProjectNumber { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public bool IsMainProject { get; set; }
    public int? MainProjectNumber { get; set; }
    public int? CustomerNumber { get; set; }
    public int? ResponsibleEmployeeNumber { get; set; }
    public int? DepartmentNumber { get; set; }
    public int? StatusNumber { get; set; }
    public string? Description { get; set; }
    public bool IsBarred { get; set; }
    public bool IsClosed { get; set; }
    public DateTime? DeliveryDate { get; set; }
    public DateTime? ClosedDate { get; set; }

    // From project_metadata (null when no metadata row exists yet)
    public int? OriginalOfferId { get; set; }
    public string? OriginalOfferNumber { get; set; }
    public decimal? BudgetHours { get; set; }
    public decimal? BudgetRevenue { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
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
    public string? Notes { get; set; }
    public string? SizeDescription { get; set; }
    public string? ResponsibleInitials { get; set; }
    public string? ResponsibleOfficeCode { get; set; }
    public decimal? EntrepriseSum { get; set; }
    public string? EntrepriseForm { get; set; }
    public decimal? ArealM2 { get; set; }
    public string? Raadgivningsform { get; set; }
    public string? Rolle { get; set; }
    public string? ByghherreKontaktperson { get; set; }
    public int? CompetitionFormId { get; set; }
    public int? EnterpriseFormId { get; set; }
    public int? ConsultantFormId { get; set; }
    public int? ProjectTypeId { get; set; }
    public int? ProjectRoleId { get; set; }
    public int? ComplexityLevelId { get; set; }
    public int? EngineeringDisciplineId { get; set; }
    public string? EngineeringDisciplineName { get; set; }
    public string? ProjectArchiveUrl { get; set; }
    public string? ProjectArchiveSiteId { get; set; }
    public string? ProjectArchiveDriveId { get; set; }
    public string? ProjectArchiveOutlookFolderId { get; set; }
    public string? OfferCaseUrl { get; set; }
    public string? OfferCasePath { get; set; }
    public string? OfferCaseDriveId { get; set; }
    public string? OfferCaseFolderItemId { get; set; }
    public string? OfferCaseOutlookFolderId { get; set; }
    public IReadOnlyList<ProjectPartnerDto> Partners { get; set; } = [];
    public IReadOnlyList<int> SegmentIds { get; set; } = [];
    public IReadOnlyList<string> Segments { get; set; } = [];

    // Project address — from DAWA lookup
    public string? ProjectDawaId { get; set; }
    public string? ProjectStreetAddress { get; set; }
    public string? ProjectPostalCode { get; set; }
    public string? ProjectCity { get; set; }
    public string? ProjectMunicipality { get; set; }
    public string? ProjectRegion { get; set; }
}