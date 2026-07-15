using System.ComponentModel.DataAnnotations;

namespace Vita.Planning.Application.DTOs;

public sealed class CreateOfferRequest
{
    [Required]
    [MaxLength(20)]
    public string OfferNumber { get; set; } = string.Empty;

    [Required]
    [MaxLength(255)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(20)]
    public string? ResponsibleInitials { get; set; }

    [MaxLength(20)]
    public string? ResponsibleOfficeCode { get; set; }

    [MaxLength(100)]
    public string? ProjectType { get; set; }

    public decimal? FeeAmount { get; set; }
    public int? ExpectedStartYear { get; set; }
    public int? ExpectedStartQuarter { get; set; }
    public int? ExpectedEndYear { get; set; }
    public int? ExpectedEndQuarter { get; set; }
    public decimal? EstimatedTotalHours { get; set; }
    public decimal? WeightedHoursOverride { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    [MaxLength(255)]
    public string? SizeDescription { get; set; }

    public bool AddToPqCompetition { get; set; }
    public DateOnly? EstimatedCompetitionStartDate { get; set; }
    public DateOnly? PqSubmissionDate { get; set; }
    public bool? DeliveredToPq { get; set; }
    public bool HasRelation { get; set; }

    public int? ConvertedToProjectNumber { get; set; }
    public DateTime? ConvertedAtUtc { get; set; }

    public int? OfferStatusId { get; set; }
    public int? CustomerId { get; set; }
    public int? CustomerContactId { get; set; }

    public List<OfferPartnerRequest> Partners { get; set; } = [];

    public bool IsActive { get; set; }

    [MaxLength(100)]
    public string? CreatedBy { get; set; }

    // Planning metadata
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

    [MaxLength(255)]
    public string? LastPlanningReviewBy { get; set; }

    public int? Priority { get; set; }
    public bool IsBillableForPlanning { get; set; }
    public bool IsAbsence { get; set; }
    public bool IsInternal { get; set; }
    public bool IsProbableCase { get; set; }
    public bool IsVisibleInPlanner { get; set; }
    public bool DailyPlanningEnabled { get; set; }
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
}
