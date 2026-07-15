using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using Vita.Planning.Application.DTOs;
using Vita.Planning.Application.Interfaces;
using Vita.Planning.Infrastructure.Data;
using Vita.Planning.Infrastructure.Data.Entities;

namespace Vita.Planning.Infrastructure.Services;

public sealed class ProjectMetadataService : IProjectMetadataService
{
    private readonly PlanningDbContext _dbContext;
    private readonly IEntityChangeLogService _changeLog;

    public ProjectMetadataService(
        PlanningDbContext dbContext,
        IEntityChangeLogService changeLog)
    {
        _dbContext = dbContext;
        _changeLog = changeLog;
    }

    public async Task<IReadOnlyList<ProjectMetadataDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.ProjectMetadata
            .AsNoTracking()
            .OrderBy(x => x.ProjectMetadataId)
            .Select(MapToDtoExpression())
            .ToListAsync(cancellationToken);
    }

    public async Task<ProjectMetadataDto?> GetByProjectNumberAsync(int projectNumber, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.ProjectMetadata
            .AsNoTracking()
            .Where(x => x.ProjectNumber == projectNumber)
            .FirstOrDefaultAsync(cancellationToken);

        if (entity is null) return null;
        return await MapToDtoWithLookupsAsync(entity, cancellationToken);
    }

    public async Task<ProjectMetadataDto> UpsertForProjectAsync(
        int projectNumber,
        UpsertProjectMetadataRequest request,
        CallerInfo caller,
        CancellationToken cancellationToken = default)
    {
        await ValidateLookupIdsAsync(request, cancellationToken);

        var entity = await _dbContext.ProjectMetadata
            .FirstOrDefaultAsync(x => x.ProjectNumber == projectNumber, cancellationToken);

        var planningTargetId = await ResolvePlanningTargetIdForProjectAsync(projectNumber, cancellationToken);
        var oldSnapshot = entity is null
            ? null
            : await BuildMetadataSnapshotAsync(entity, planningTargetId, cancellationToken);
        var now = DateTime.UtcNow;
        var actor = ResolveActor(caller);
        var wasCreated = entity is null;

        if (entity is null)
        {
            entity = new ProjectMetadata
            {
                ProjectNumber = projectNumber,
                CreatedBy = actor,
                CreatedAtUtc = now
            };
            _dbContext.ProjectMetadata.Add(entity);
        }
        else
        {
            entity.UpdatedAtUtc = now;
            entity.UpdatedBy = actor;
        }

        ApplyRequest(entity, request);

        await _dbContext.SaveChangesAsync(cancellationToken);
        await SyncSegmentsAsync(entity.ProjectMetadataId, request.SegmentIds, cancellationToken);

        var result = await MapToDtoWithLookupsAsync(entity, cancellationToken);
        await _changeLog.RecordChangeAsync(new RecordEntityChangeRequest
        {
            EventType = wasCreated ? "ProjectMetadataCreated" : "ProjectMetadataUpdated",
            EventTitle = wasCreated
                ? $"Projektmetadata oprettet: {projectNumber}"
                : $"Projektmetadata ændret: {projectNumber}",
            EntityType = "ProjectMetadata",
            EntityId = entity.ProjectMetadataId.ToString(),
            PlanningTargetId = planningTargetId,
            OldValue = oldSnapshot?.PlanningStatus,
            NewValue = result.PlanningStatus,
            OldSnapshot = oldSnapshot,
            NewSnapshot = result,
            ChangeReason = "UpsertProjectMetadata",
            Caller = caller,
            SourceModule = "ProjectMetadataService"
        }, cancellationToken);

        return result;
    }

    private async Task ValidateLookupIdsAsync(UpsertProjectMetadataRequest request, CancellationToken ct)
    {
        if (request.CompetitionFormId.HasValue)
        {
            var exists = await _dbContext.CompetitionForms
                .AnyAsync(x => x.CompetitionFormId == request.CompetitionFormId.Value && x.IsActive, ct);
            if (!exists)
                throw new InvalidOperationException($"CompetitionForm with id {request.CompetitionFormId.Value} does not exist.");
        }
        if (request.EnterpriseFormId.HasValue)
        {
            var exists = await _dbContext.EnterpriseForms
                .AnyAsync(x => x.EnterpriseFormId == request.EnterpriseFormId.Value && x.IsActive, ct);
            if (!exists)
                throw new InvalidOperationException($"EnterpriseForm with id {request.EnterpriseFormId.Value} does not exist.");
        }
        if (request.ConsultantFormId.HasValue)
        {
            var exists = await _dbContext.ConsultantForms
                .AnyAsync(x => x.ConsultantFormId == request.ConsultantFormId.Value && x.IsActive, ct);
            if (!exists)
                throw new InvalidOperationException($"ConsultantForm with id {request.ConsultantFormId.Value} does not exist.");
        }
        if (request.ProjectTypeId.HasValue)
        {
            var exists = await _dbContext.ProjectTypes
                .AnyAsync(x => x.ProjectTypeId == request.ProjectTypeId.Value && x.IsActive, ct);
            if (!exists)
                throw new InvalidOperationException($"ProjectType with id {request.ProjectTypeId.Value} does not exist.");
        }
        if (request.ProjectRoleId.HasValue)
        {
            var exists = await _dbContext.ProjectRoles
                .AnyAsync(x => x.ProjectRoleId == request.ProjectRoleId.Value && x.IsActive, ct);
            if (!exists)
                throw new InvalidOperationException($"ProjectRole with id {request.ProjectRoleId.Value} does not exist.");
        }
        if (request.ComplexityLevelId.HasValue)
        {
            var exists = await _dbContext.ComplexityLevels
                .AnyAsync(x => x.ComplexityLevelId == request.ComplexityLevelId.Value && x.IsActive, ct);
            if (!exists)
                throw new InvalidOperationException($"ComplexityLevel with id {request.ComplexityLevelId.Value} does not exist.");
        }
        if (request.EngineeringDisciplineId.HasValue)
        {
            var exists = await _dbContext.EngineeringDisciplines
                .AnyAsync(x => x.EngineeringDisciplineId == request.EngineeringDisciplineId.Value && x.IsActive, ct);
            if (!exists)
                throw new InvalidOperationException($"EngineeringDiscipline with id {request.EngineeringDisciplineId.Value} does not exist.");
        }
        if (request.SegmentIds.Count > 0)
        {
            var validIds = await _dbContext.Segments
                .Where(x => x.IsActive)
                .Select(x => x.SegmentId)
                .ToListAsync(ct);

            var invalidIds = request.SegmentIds.Except(validIds).ToList();
            if (invalidIds.Count > 0)
                throw new InvalidOperationException($"Segment ids {string.Join(", ", invalidIds)} do not exist.");
        }
    }

    private static void ApplyRequest(ProjectMetadata entity, UpsertProjectMetadataRequest request)
    {
        entity.PlanningCategory = NormalizeNullable(request.PlanningCategory);
        entity.PlanningStatus = NormalizeNullable(request.PlanningStatus);
        entity.DisciplineOwner = NormalizeNullable(request.DisciplineOwner);
        entity.DefaultDescription = NormalizeNullable(request.DefaultDescription);
        entity.ColorTag = NormalizeNullable(request.ColorTag);
        entity.PlanningGroup = NormalizeNullable(request.PlanningGroup);
        entity.Phase = NormalizeNullable(request.Phase);
        entity.ProbabilityPercent = request.ProbabilityPercent;
        entity.BudgetHours = request.BudgetHours;
        entity.BudgetRevenue = request.BudgetRevenue;
        entity.LastPlanningReviewBy = NormalizeNullable(request.LastPlanningReviewBy);
        entity.Priority = request.Priority;
        entity.IsBillableForPlanning = request.IsBillableForPlanning;
        entity.IsAbsence = request.IsAbsence;
        entity.IsInternal = request.IsInternal;
        entity.IsProbableCase = request.IsProbableCase;
        entity.IsVisibleInPlanner = request.IsVisibleInPlanner;
        entity.DailyPlanningEnabled = request.DailyPlanningEnabled;
        entity.StartDate = request.StartDate;
        entity.EndDate = request.EndDate;
        entity.Notes = NormalizeNullable(request.Notes);
        entity.SizeDescription = NormalizeNullable(request.SizeDescription);
        entity.ResponsibleInitials = NormalizeNullable(request.ResponsibleInitials);
        entity.ResponsibleOfficeCode = NormalizeNullable(request.ResponsibleOfficeCode);
        entity.EntrepriseSum = request.EntrepriseSum;
        entity.EntrepriseForm = NormalizeNullable(request.EntrepriseForm);
        entity.ArealM2 = request.ArealM2;
        entity.Raadgivningsform = NormalizeNullable(request.Raadgivningsform);
        entity.Rolle = NormalizeNullable(request.Rolle);
        entity.ByghherreKontaktperson = NormalizeNullable(request.ByghherreKontaktperson);
        entity.CompetitionFormId = request.CompetitionFormId;
        entity.EnterpriseFormId = request.EnterpriseFormId;
        entity.ConsultantFormId = request.ConsultantFormId;
        entity.ProjectTypeId = request.ProjectTypeId;
        entity.ProjectRoleId = request.ProjectRoleId;
        entity.ComplexityLevelId = request.ComplexityLevelId;
        entity.EngineeringDisciplineId = request.EngineeringDisciplineId;
        entity.ProjectDawaId = NormalizeNullable(request.ProjectDawaId);
        entity.ProjectStreetAddress = NormalizeNullable(request.ProjectStreetAddress);
        entity.ProjectPostalCode = NormalizeNullable(request.ProjectPostalCode);
        entity.ProjectCity = NormalizeNullable(request.ProjectCity);
        entity.ProjectMunicipality = NormalizeNullable(request.ProjectMunicipality);
        entity.ProjectRegion = NormalizeNullable(request.ProjectRegion);
        // Archive/Graph IDs managed by dedicated folder-creation endpoints; only overwrite if provided.
        if (request.ProjectArchiveUrl != null)
            entity.ProjectArchiveUrl = NormalizeNullable(request.ProjectArchiveUrl);
        if (request.ProjectArchiveSiteId != null)
            entity.ProjectArchiveSiteId = NormalizeNullable(request.ProjectArchiveSiteId);
        if (request.ProjectArchiveDriveId != null)
            entity.ProjectArchiveDriveId = NormalizeNullable(request.ProjectArchiveDriveId);
        if (request.ProjectArchiveOutlookFolderId != null)
            entity.ProjectArchiveOutlookFolderId = NormalizeNullable(request.ProjectArchiveOutlookFolderId);
    }

    private static string? NormalizeNullable(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string ResolveActor(CallerInfo caller) =>
        !string.IsNullOrWhiteSpace(caller.UserId)
            ? caller.UserId
            : !string.IsNullOrWhiteSpace(caller.Email)
                ? caller.Email
                : caller.Name;

    private async Task<int?> ResolvePlanningTargetIdForProjectAsync(int projectNumber, CancellationToken ct) =>
        await _dbContext.PlanningTargets
            .Where(x => x.ExtProjectNumber == projectNumber)
            .Select(x => (int?)x.PlanningTargetId)
            .FirstOrDefaultAsync(ct);

    private async Task SyncSegmentsAsync(int projectMetadataId, IReadOnlyList<int> segmentIds, CancellationToken ct)
    {
        var existing = await _dbContext.ProjectMetadataSegments
            .Where(x => x.ProjectMetadataId == projectMetadataId)
            .ToListAsync(ct);

        _dbContext.ProjectMetadataSegments.RemoveRange(existing);

        foreach (var segId in segmentIds.Distinct())
        {
            _dbContext.ProjectMetadataSegments.Add(new ProjectMetadataSegment
            {
                ProjectMetadataId = projectMetadataId,
                SegmentId = segId
            });
        }

        await _dbContext.SaveChangesAsync(ct);
    }

    private async Task<ProjectMetadataDto> BuildMetadataSnapshotAsync(
        ProjectMetadata entity,
        int? planningTargetId,
        CancellationToken ct)
    {
        var dto = MapToDto(entity);

        var segments = await _dbContext.ProjectMetadataSegments
            .AsNoTracking()
            .Where(x => x.ProjectMetadataId == entity.ProjectMetadataId)
            .Join(_dbContext.Segments.AsNoTracking(), pms => pms.SegmentId, s => s.SegmentId,
                (pms, s) => new { pms.SegmentId, s.Name })
            .ToListAsync(ct);

        dto.SegmentIds = segments.Select(x => x.SegmentId).ToList();
        dto.Segments = segments.Select(x => x.Name).ToList();
        return dto;
    }

    private async Task<ProjectMetadataDto> MapToDtoWithLookupsAsync(ProjectMetadata entity, CancellationToken ct)
    {
        var dto = MapToDto(entity);

        if (entity.CompetitionFormId.HasValue)
        {
            dto.CompetitionFormName = await _dbContext.CompetitionForms
                .Where(x => x.CompetitionFormId == entity.CompetitionFormId)
                .Select(x => x.Name).FirstOrDefaultAsync(ct);
        }
        if (entity.EnterpriseFormId.HasValue)
        {
            dto.EnterpriseFormName = await _dbContext.EnterpriseForms
                .Where(x => x.EnterpriseFormId == entity.EnterpriseFormId)
                .Select(x => x.Name).FirstOrDefaultAsync(ct);
        }
        if (entity.ConsultantFormId.HasValue)
        {
            dto.ConsultantFormName = await _dbContext.ConsultantForms
                .Where(x => x.ConsultantFormId == entity.ConsultantFormId)
                .Select(x => x.Name).FirstOrDefaultAsync(ct);
        }
        if (entity.ProjectTypeId.HasValue)
        {
            dto.ProjectTypeName = await _dbContext.ProjectTypes
                .Where(x => x.ProjectTypeId == entity.ProjectTypeId)
                .Select(x => x.Name).FirstOrDefaultAsync(ct);
        }
        if (entity.ProjectRoleId.HasValue)
        {
            dto.ProjectRoleName = await _dbContext.ProjectRoles
                .Where(x => x.ProjectRoleId == entity.ProjectRoleId)
                .Select(x => x.Name).FirstOrDefaultAsync(ct);
        }
        if (entity.ComplexityLevelId.HasValue)
        {
            dto.ComplexityLevelName = await _dbContext.ComplexityLevels
                .Where(x => x.ComplexityLevelId == entity.ComplexityLevelId)
                .Select(x => x.Name).FirstOrDefaultAsync(ct);
        }
        if (entity.EngineeringDisciplineId.HasValue)
        {
            dto.EngineeringDisciplineName = await _dbContext.EngineeringDisciplines
                .Where(x => x.EngineeringDisciplineId == entity.EngineeringDisciplineId)
                .Select(x => x.Name).FirstOrDefaultAsync(ct);
        }

        var segments = await _dbContext.ProjectMetadataSegments
            .Where(x => x.ProjectMetadataId == entity.ProjectMetadataId)
            .Join(_dbContext.Segments, pms => pms.SegmentId, s => s.SegmentId,
                (pms, s) => new { pms.SegmentId, s.Name })
            .ToListAsync(ct);

        dto.SegmentIds = segments.Select(x => x.SegmentId).ToList();
        dto.Segments = segments.Select(x => x.Name).ToList();

        return dto;
    }

    private static ProjectMetadataDto MapToDto(ProjectMetadata entity) => new()
    {
        ProjectMetadataId = entity.ProjectMetadataId,
        ProjectNumber = entity.ProjectNumber,
        OriginalOfferId = entity.OriginalOfferId,
        OriginalOfferNumber = entity.OriginalOfferNumber,
        PlanningCategory = entity.PlanningCategory,
        PlanningStatus = entity.PlanningStatus,
        DisciplineOwner = entity.DisciplineOwner,
        DefaultDescription = entity.DefaultDescription,
        ColorTag = entity.ColorTag,
        PlanningGroup = entity.PlanningGroup,
        Phase = entity.Phase,
        ProbabilityPercent = entity.ProbabilityPercent,
        BudgetHours = entity.BudgetHours,
        BudgetRevenue = entity.BudgetRevenue,
        LastPlanningReviewBy = entity.LastPlanningReviewBy,
        Priority = entity.Priority,
        IsBillableForPlanning = entity.IsBillableForPlanning,
        IsAbsence = entity.IsAbsence,
        IsInternal = entity.IsInternal,
        IsProbableCase = entity.IsProbableCase,
        IsVisibleInPlanner = entity.IsVisibleInPlanner,
        DailyPlanningEnabled = entity.DailyPlanningEnabled,
        StartDate = entity.StartDate,
        EndDate = entity.EndDate,
        Notes = entity.Notes,
        SizeDescription = entity.SizeDescription,
        ResponsibleInitials = entity.ResponsibleInitials,
        ResponsibleOfficeCode = entity.ResponsibleOfficeCode,
        CreatedBy = entity.CreatedBy,
        UpdatedBy = entity.UpdatedBy,
        CreatedAtUtc = entity.CreatedAtUtc,
        UpdatedAtUtc = entity.UpdatedAtUtc,
        EntrepriseSum = entity.EntrepriseSum,
        EntrepriseForm = entity.EntrepriseForm,
        ArealM2 = entity.ArealM2,
        Raadgivningsform = entity.Raadgivningsform,
        Rolle = entity.Rolle,
        ByghherreKontaktperson = entity.ByghherreKontaktperson,
        CompetitionFormId = entity.CompetitionFormId,
        EnterpriseFormId = entity.EnterpriseFormId,
        ConsultantFormId = entity.ConsultantFormId,
        ProjectTypeId = entity.ProjectTypeId,
        ProjectRoleId = entity.ProjectRoleId,
        ComplexityLevelId = entity.ComplexityLevelId,
        EngineeringDisciplineId = entity.EngineeringDisciplineId,
        ProjectArchiveUrl = entity.ProjectArchiveUrl,
        ProjectArchiveSiteId = entity.ProjectArchiveSiteId,
        ProjectArchiveDriveId = entity.ProjectArchiveDriveId,
        ProjectArchiveOutlookFolderId = entity.ProjectArchiveOutlookFolderId,
        OfferCaseUrl = entity.OfferCaseUrl,
        OfferCasePath = entity.OfferCasePath,
        OfferCaseDriveId = entity.OfferCaseDriveId,
        OfferCaseFolderItemId = entity.OfferCaseFolderItemId,
        OfferCaseOutlookFolderId = entity.OfferCaseOutlookFolderId,
        ProjectDawaId = entity.ProjectDawaId,
        ProjectStreetAddress = entity.ProjectStreetAddress,
        ProjectPostalCode = entity.ProjectPostalCode,
        ProjectCity = entity.ProjectCity,
        ProjectMunicipality = entity.ProjectMunicipality,
        ProjectRegion = entity.ProjectRegion
    };

    private static Expression<Func<ProjectMetadata, ProjectMetadataDto>> MapToDtoExpression() =>
        entity => new ProjectMetadataDto
        {
            ProjectMetadataId = entity.ProjectMetadataId,
            ProjectNumber = entity.ProjectNumber,
            OriginalOfferId = entity.OriginalOfferId,
            OriginalOfferNumber = entity.OriginalOfferNumber,
            PlanningCategory = entity.PlanningCategory,
            PlanningStatus = entity.PlanningStatus,
            DisciplineOwner = entity.DisciplineOwner,
            DefaultDescription = entity.DefaultDescription,
            ColorTag = entity.ColorTag,
            PlanningGroup = entity.PlanningGroup,
            Phase = entity.Phase,
            ProbabilityPercent = entity.ProbabilityPercent,
            BudgetHours = entity.BudgetHours,
            BudgetRevenue = entity.BudgetRevenue,
            LastPlanningReviewBy = entity.LastPlanningReviewBy,
            Priority = entity.Priority,
            IsBillableForPlanning = entity.IsBillableForPlanning,
            IsAbsence = entity.IsAbsence,
            IsInternal = entity.IsInternal,
            IsProbableCase = entity.IsProbableCase,
            IsVisibleInPlanner = entity.IsVisibleInPlanner,
            DailyPlanningEnabled = entity.DailyPlanningEnabled,
            StartDate = entity.StartDate,
            EndDate = entity.EndDate,
            Notes = entity.Notes,
            SizeDescription = entity.SizeDescription,
            ResponsibleInitials = entity.ResponsibleInitials,
            ResponsibleOfficeCode = entity.ResponsibleOfficeCode,
            CreatedBy = entity.CreatedBy,
            UpdatedBy = entity.UpdatedBy,
            CreatedAtUtc = entity.CreatedAtUtc,
            UpdatedAtUtc = entity.UpdatedAtUtc,
            EntrepriseSum = entity.EntrepriseSum,
            EntrepriseForm = entity.EntrepriseForm,
            ArealM2 = entity.ArealM2,
            Raadgivningsform = entity.Raadgivningsform,
            Rolle = entity.Rolle,
            ByghherreKontaktperson = entity.ByghherreKontaktperson,
            CompetitionFormId = entity.CompetitionFormId,
            EnterpriseFormId = entity.EnterpriseFormId,
            ConsultantFormId = entity.ConsultantFormId,
            ProjectTypeId = entity.ProjectTypeId,
            ProjectRoleId = entity.ProjectRoleId,
            ComplexityLevelId = entity.ComplexityLevelId,
            EngineeringDisciplineId = entity.EngineeringDisciplineId,
            ProjectArchiveUrl = entity.ProjectArchiveUrl,
            ProjectArchiveSiteId = entity.ProjectArchiveSiteId,
            ProjectArchiveDriveId = entity.ProjectArchiveDriveId,
            ProjectArchiveOutlookFolderId = entity.ProjectArchiveOutlookFolderId,
            OfferCaseUrl = entity.OfferCaseUrl,
            OfferCasePath = entity.OfferCasePath,
            OfferCaseDriveId = entity.OfferCaseDriveId,
            OfferCaseFolderItemId = entity.OfferCaseFolderItemId,
            OfferCaseOutlookFolderId = entity.OfferCaseOutlookFolderId,
            ProjectDawaId = entity.ProjectDawaId,
            ProjectStreetAddress = entity.ProjectStreetAddress,
            ProjectPostalCode = entity.ProjectPostalCode,
            ProjectCity = entity.ProjectCity,
            ProjectMunicipality = entity.ProjectMunicipality,
            ProjectRegion = entity.ProjectRegion
        };
}
