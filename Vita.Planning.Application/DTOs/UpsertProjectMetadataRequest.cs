using System.ComponentModel.DataAnnotations;

namespace Vita.Planning.Application.DTOs;

public sealed class UpsertProjectMetadataRequest
{
    [MaxLength(100)]
    public string? PlanningCategory { get; set; }

    [MaxLength(100)]
    public string? PlanningStatus { get; set; }

    [MaxLength(100)]
    public string? DisciplineOwner { get; set; }

    public string? DefaultDescription { get; set; }

    [MaxLength(50)]
    public string? ColorTag { get; set; }

    [MaxLength(100)]
    public string? PlanningGroup { get; set; }

    [MaxLength(100)]
    public string? Phase { get; set; }

    public decimal? ProbabilityPercent { get; set; }
    public decimal? BudgetHours { get; set; }
    public decimal? BudgetRevenue { get; set; }

    [MaxLength(255)]
    public string? LastPlanningReviewBy { get; set; }

    public int? Priority { get; set; }
    public bool IsBillableForPlanning { get; set; }
    public bool IsAbsence { get; set; }
    public bool IsInternal { get; set; }
    public bool IsProbableCase { get; set; }
    public bool IsVisibleInPlanner { get; set; }
    public bool DailyPlanningEnabled { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    [MaxLength(255)]
    public string? SizeDescription { get; set; }

    [MaxLength(20)]
    public string? ResponsibleInitials { get; set; }

    [MaxLength(20)]
    public string? ResponsibleOfficeCode { get; set; }

    [MaxLength(100)]
    public string? UpdatedBy { get; set; }

    public decimal? EntrepriseSum { get; set; }

    [MaxLength(255)]
    public string? EntrepriseForm { get; set; }

    public decimal? ArealM2 { get; set; }

    [MaxLength(255)]
    public string? Raadgivningsform { get; set; }

    [MaxLength(255)]
    public string? Rolle { get; set; }

    [MaxLength(255)]
    public string? ByghherreKontaktperson { get; set; }

    public int? CompetitionFormId { get; set; }
    public int? EnterpriseFormId { get; set; }
    public int? ConsultantFormId { get; set; }
    public int? ProjectTypeId { get; set; }
    public int? ProjectRoleId { get; set; }
    public int? ComplexityLevelId { get; set; }
    public int? EngineeringDisciplineId { get; set; }

    public IReadOnlyList<int> SegmentIds { get; set; } = [];

    // Project archive links — managed by dedicated folder-creation endpoints.
    // Only overwrite when the caller explicitly provides a value.
    public string? ProjectArchiveUrl { get; set; }

    [MaxLength(255)]
    public string? ProjectArchiveSiteId { get; set; }

    [MaxLength(255)]
    public string? ProjectArchiveDriveId { get; set; }

    [MaxLength(500)]
    public string? ProjectArchiveOutlookFolderId { get; set; }

    // Project address — from DAWA lookup
    [MaxLength(50)]
    public string? ProjectDawaId { get; set; }

    [MaxLength(255)]
    public string? ProjectStreetAddress { get; set; }

    [MaxLength(10)]
    public string? ProjectPostalCode { get; set; }

    [MaxLength(100)]
    public string? ProjectCity { get; set; }

    [MaxLength(100)]
    public string? ProjectMunicipality { get; set; }

    [MaxLength(100)]
    public string? ProjectRegion { get; set; }
}
