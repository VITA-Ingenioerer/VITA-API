using Microsoft.EntityFrameworkCore;
using Vita.Planning.Application.DTOs;
using Vita.Planning.Application.Interfaces;
using Vita.Planning.Infrastructure.Data;

namespace Vita.Planning.Infrastructure.Services;

public sealed class ProjectQueryService : IProjectQueryService
{
    private readonly PlanningDbContext _db;

    public ProjectQueryService(PlanningDbContext db)
    {
        _db = db;
    }

    public async Task<PagedResultDto<ProjectListItemDto>> GetProjectsAsync(int page, int pageSize, string? query = null, CancellationToken cancellationToken = default)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize < 1 ? 100 : Math.Min(pageSize, 500);

        var dbQuery = _db.Projects.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query))
        {
            var q = query.Trim();
            if (int.TryParse(q, out var projectNumber))
                dbQuery = dbQuery.Where(p => p.ProjectName.Contains(q) || p.ProjectNumber == projectNumber);
            else
                dbQuery = dbQuery.Where(p => p.ProjectName.Contains(q));
        }

        var totalCount = await dbQuery.CountAsync(cancellationToken);

        var items = await dbQuery
            .GroupJoin(
                _db.ProjectMetadata.AsNoTracking(),
                p => p.ProjectNumber,
                m => m.ProjectNumber,
                (p, metas) => new { p, metas })
            .SelectMany(
                x => x.metas.DefaultIfEmpty(),
                (x, meta) => new { x.p, meta })
            .OrderByDescending(x => x.meta == null ? (DateTime?)null : x.meta.CreatedAtUtc)
            .ThenByDescending(x => x.p.ProjectNumber)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new ProjectListItemDto
            {
                ProjectNumber = x.p.ProjectNumber,
                ProjectName = x.p.ProjectName,
                IsMainProject = x.p.IsMainProject,
                MainProjectNumber = x.p.MainProjectNumber,
                ResponsibleEmployeeNumber = x.p.ResponsibleEmployeeNumber,
                IsClosed = x.p.IsClosed,
                IsBarred = x.p.IsBarred,
                DeliveryDate = x.p.DeliveryDate
            })
            .ToListAsync(cancellationToken);

        return new PagedResultDto<ProjectListItemDto>
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize),
            Items = items
        };
    }

    public async Task<ProjectDetailsDto?> GetProjectByNumberAsync(int projectNumber, CancellationToken cancellationToken = default)
    {
        var project = await _db.Projects
            .AsNoTracking()
            .Where(p => p.ProjectNumber == projectNumber)
            .FirstOrDefaultAsync(cancellationToken);

        if (project is null)
            return null;

        var meta = await _db.ProjectMetadata
            .AsNoTracking()
            .Where(m => m.ProjectNumber == projectNumber)
            .FirstOrDefaultAsync(cancellationToken);

        var planningTargetId = await _db.PlanningTargets
            .AsNoTracking()
            .Where(pt => pt.ExtProjectNumber == projectNumber)
            .Select(pt => (int?)pt.PlanningTargetId)
            .FirstOrDefaultAsync(cancellationToken);

        IReadOnlyList<ProjectPartnerDto> partners = [];
        if (planningTargetId.HasValue)
            partners = await LoadProjectPartnersAsync(planningTargetId.Value, cancellationToken);

        IReadOnlyList<int> segmentIds = [];
        IReadOnlyList<string> segmentNames = [];
        if (meta is not null)
        {
            var segs = await _db.ProjectMetadataSegments
                .AsNoTracking()
                .Where(s => s.ProjectMetadataId == meta.ProjectMetadataId)
                .Join(_db.Segments, s => s.SegmentId, seg => seg.SegmentId,
                    (s, seg) => new { s.SegmentId, seg.Name })
                .ToListAsync(cancellationToken);
            segmentIds = segs.Select(s => s.SegmentId).ToList();
            segmentNames = segs.Select(s => s.Name).ToList();
        }

        return new ProjectDetailsDto
        {
            ProjectNumber = project.ProjectNumber,
            ProjectName = project.ProjectName,
            IsMainProject = project.IsMainProject,
            MainProjectNumber = project.MainProjectNumber,
            CustomerNumber = project.CustomerNumber,
            ResponsibleEmployeeNumber = project.ResponsibleEmployeeNumber,
            DepartmentNumber = project.DepartmentNumber,
            StatusNumber = project.StatusNumber,
            Description = project.Description,
            IsBarred = project.IsBarred,
            IsClosed = project.IsClosed,
            DeliveryDate = project.DeliveryDate,
            ClosedDate = project.ClosedDate,
            OriginalOfferId = meta?.OriginalOfferId,
            OriginalOfferNumber = meta?.OriginalOfferNumber,
            BudgetHours = meta?.BudgetHours,
            BudgetRevenue = meta?.BudgetRevenue,
            StartDate = meta?.StartDate,
            EndDate = meta?.EndDate,
            PlanningCategory = meta?.PlanningCategory,
            PlanningStatus = meta?.PlanningStatus,
            DisciplineOwner = meta?.DisciplineOwner,
            DefaultDescription = meta?.DefaultDescription,
            ColorTag = meta?.ColorTag,
            PlanningGroup = meta?.PlanningGroup,
            Phase = meta?.Phase,
            ProbabilityPercent = meta?.ProbabilityPercent,
            LastPlanningReviewBy = meta?.LastPlanningReviewBy,
            Priority = meta?.Priority,
            IsBillableForPlanning = meta?.IsBillableForPlanning ?? false,
            IsAbsence = meta?.IsAbsence ?? false,
            IsInternal = meta?.IsInternal ?? false,
            IsProbableCase = meta?.IsProbableCase ?? false,
            IsVisibleInPlanner = meta?.IsVisibleInPlanner ?? false,
            DailyPlanningEnabled = meta?.DailyPlanningEnabled ?? false,
            Notes = meta?.Notes,
            SizeDescription = meta?.SizeDescription,
            ResponsibleInitials = meta?.ResponsibleInitials,
            ResponsibleOfficeCode = meta?.ResponsibleOfficeCode,
            EntrepriseSum = meta?.EntrepriseSum,
            EntrepriseForm = meta?.EntrepriseForm,
            ArealM2 = meta?.ArealM2,
            Raadgivningsform = meta?.Raadgivningsform,
            Rolle = meta?.Rolle,
            ByghherreKontaktperson = meta?.ByghherreKontaktperson,
            CompetitionFormId = meta?.CompetitionFormId,
            EnterpriseFormId = meta?.EnterpriseFormId,
            ConsultantFormId = meta?.ConsultantFormId,
            ProjectTypeId = meta?.ProjectTypeId,
            ProjectRoleId = meta?.ProjectRoleId,
            ComplexityLevelId = meta?.ComplexityLevelId,
            EngineeringDisciplineId = meta?.EngineeringDisciplineId,
            ProjectArchiveUrl = meta?.ProjectArchiveUrl,
            ProjectArchiveSiteId = meta?.ProjectArchiveSiteId,
            ProjectArchiveDriveId = meta?.ProjectArchiveDriveId,
            ProjectArchiveOutlookFolderId = meta?.ProjectArchiveOutlookFolderId,
            OfferCaseUrl = meta?.OfferCaseUrl,
            OfferCasePath = meta?.OfferCasePath,
            OfferCaseDriveId = meta?.OfferCaseDriveId,
            OfferCaseFolderItemId = meta?.OfferCaseFolderItemId,
            OfferCaseOutlookFolderId = meta?.OfferCaseOutlookFolderId,
            Partners = partners,
            SegmentIds = segmentIds,
            Segments = segmentNames,
        };
    }

    private async Task<IReadOnlyList<ProjectPartnerDto>> LoadProjectPartnersAsync(
        int planningTargetId,
        CancellationToken cancellationToken)
    {
        return await (
            from r in _db.CustomerPartnerRoles.AsNoTracking()
                .Where(x => x.PlanningTargetId == planningTargetId)
            join c in _db.Customers on r.CustomerId equals c.CustomerId
            join rt in _db.PlanningPartnerRoleTypes on r.PlanningPartnerRoleTypeId equals rt.PlanningPartnerRoleTypeId
            from cc in _db.CompanyContacts
                .Where(x => x.CompanyContactId == r.CompanyContactId)
                .DefaultIfEmpty()
            orderby rt.Name
            select new ProjectPartnerDto
            {
                CustomerPartnerRoleId = r.CustomerPartnerRoleId,
                CustomerId = r.CustomerId,
                CustomerName = c.Name,
                CvrNumber = c.CvrNumber,
                RoleTypeId = rt.PlanningPartnerRoleTypeId,
                RoleTypeName = rt.Name,
                CustomerContactId = r.CompanyContactId,
                ContactPersonName = cc == null ? null : cc.Name,
                ContactPersonEmail = cc == null ? null : cc.Email,
                ContactPersonPhone = cc == null ? null : cc.Phone
            }
        ).ToListAsync(cancellationToken);
    }

}