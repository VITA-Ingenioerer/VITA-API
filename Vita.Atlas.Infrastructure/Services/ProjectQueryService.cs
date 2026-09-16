using Microsoft.EntityFrameworkCore;
using Vita.Atlas.Application;
using Vita.Atlas.Application.DTOs;
using Vita.Atlas.Application.Interfaces;
using Vita.Atlas.Infrastructure.Data;

namespace Vita.Atlas.Infrastructure.Services;

public sealed class ProjectQueryService : IProjectQueryService
{
    private readonly AtlasDbContext _db;

    public ProjectQueryService(AtlasDbContext db)
    {
        _db = db;
    }

    public async Task<PagedResultDto<ProjectListItemDto>> GetProjectsAsync(
        int page,
        int pageSize,
        string? query = null,
        bool? isClosed = null,
        bool? isBarred = null,
        string? dawaId = null,
        bool includeLastResourcePlanActivity = false,
        CancellationToken cancellationToken = default)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize < 1 ? 100 : Math.Min(pageSize, 500);

        // Metadata joined before filtering (not after, as it used to be) — address
        // and the DAWA id only live on project_metadata, so matching on them has to
        // happen before Skip/Take, not just when shaping the page for display.
        var dbQuery = _db.Projects.AsNoTracking()
            .GroupJoin(
                _db.ProjectMetadata.AsNoTracking(),
                p => p.ProjectNumber,
                m => m.ProjectNumber,
                (p, metas) => new { p, metas })
            .SelectMany(
                x => x.metas.DefaultIfEmpty(),
                (x, meta) => new { x.p, meta });

        if (!string.IsNullOrWhiteSpace(query))
        {
            var q = query.Trim();
            var matchesNumber = int.TryParse(q, out var projectNumber);
            dbQuery = dbQuery.Where(x =>
                x.p.ProjectName.Contains(q) ||
                (matchesNumber && x.p.ProjectNumber == projectNumber) ||
                (x.meta != null && x.meta.ProjectStreetAddress != null && x.meta.ProjectStreetAddress.Contains(q)) ||
                (x.meta != null && x.meta.ProjectCity != null && x.meta.ProjectCity.Contains(q)));
        }

        // Both optional and unset by default — omitting them keeps every existing caller's
        // behavior (the full, unfiltered catalog) unchanged.
        if (isClosed.HasValue)
            dbQuery = dbQuery.Where(x => x.p.IsClosed == isClosed.Value);

        if (isBarred.HasValue)
            dbQuery = dbQuery.Where(x => x.p.IsBarred == isBarred.Value);

        if (!string.IsNullOrWhiteSpace(dawaId))
        {
            var normalizedDawaId = dawaId.Trim();
            dbQuery = dbQuery.Where(x => x.meta != null && x.meta.ProjectDawaId == normalizedDawaId);
        }

        var totalCount = await dbQuery.CountAsync(cancellationToken);

        var items = await dbQuery
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
                StatusNumber = x.p.StatusNumber,
                ProjectArchiveUrl = x.meta == null ? null : x.meta.ProjectArchiveUrl,
                IsClosed = x.p.IsClosed,
                IsBarred = x.p.IsBarred,
                DeliveryDate = x.p.DeliveryDate
            })
            .ToListAsync(cancellationToken);

        // ext.project_statuses is a handful of rows, so the names are resolved in one
        // extra round-trip and mapped in memory rather than complicating the filtered
        // GroupJoin above with another outer join.
        var statusNames = await GetStatusNamesAsync(cancellationToken);

        // Opt-in because it is by far the most expensive part of this endpoint: it groups over
        // the whole resource-plan-entry table (~270k rows) and measures around two seconds per
        // page. Only the project admin list shows the column, while the time entry app pages
        // through every project on load — it must not pay for a column it never renders.
        var lastActivity = includeLastResourcePlanActivity
            ? await GetLastResourcePlanActivityAsync(items.Select(x => x.ProjectNumber).ToList(), cancellationToken)
            : [];

        foreach (var item in items)
        {
            item.StatusName = item.StatusNumber.HasValue && statusNames.TryGetValue(item.StatusNumber.Value, out var name)
                ? name
                : null;
            item.LastResourcePlanActivityUtc = lastActivity.TryGetValue(item.ProjectNumber, out var touched)
                ? touched
                : null;
        }

        return new PagedResultDto<ProjectListItemDto>
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize),
            Items = items
        };
    }

    // "When did anyone last plan hours on this?" — the newest stamp across the project's
    // resource plan entries, reached through its planning targets. Batched over a page of
    // projects so the list costs one extra query rather than one per row.
    private async Task<Dictionary<int, DateTime>> GetLastResourcePlanActivityAsync(
        IReadOnlyList<int> projectNumbers,
        CancellationToken cancellationToken)
    {
        if (projectNumbers.Count == 0)
        {
            return [];
        }

        var rows = await (
                from entry in _db.ResourcePlanEntries.AsNoTracking()
                join target in _db.PlanningTargets.AsNoTracking()
                    on entry.PlanningTargetId equals target.PlanningTargetId
                where target.ExtProjectNumber != null && projectNumbers.Contains(target.ExtProjectNumber.Value)
                group entry by target.ExtProjectNumber!.Value into grouped
                select new
                {
                    ProjectNumber = grouped.Key,
                    // An entry that has never been edited since it was created carries only
                    // CreatedAt, so both stamps count.
                    LastTouchedUtc = grouped.Max(x => x.UpdatedAt ?? x.CreatedAt)
                })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(x => x.ProjectNumber, x => x.LastTouchedUtc);
    }

    // e-conomic owns project status; we only mirror it. Small enough to read whole and
    // cheaper than joining it into every query that wants the name.
    private async Task<Dictionary<int, string>> GetStatusNamesAsync(CancellationToken cancellationToken)
    {
        return await _db.ProjectStatuses
            .AsNoTracking()
            .Where(x => x.Name != null)
            .ToDictionaryAsync(x => x.StatusNumber, x => x.Name!, cancellationToken);
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
        IReadOnlyList<int> disciplineIds = [];
        IReadOnlyList<string> disciplineNames = [];
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

            var discs = await _db.ProjectMetadataDisciplines
                .AsNoTracking()
                .Where(d => d.ProjectMetadataId == meta.ProjectMetadataId)
                .Join(_db.EngineeringDisciplines, d => d.EngineeringDisciplineId,
                    disc => disc.EngineeringDisciplineId, (d, disc) => new { d.EngineeringDisciplineId, disc.Name })
                .ToListAsync(cancellationToken);
            disciplineIds = discs.Select(d => d.EngineeringDisciplineId).ToList();
            disciplineNames = discs.Select(d => d.Name).ToList();
        }

        var statusNames = await GetStatusNamesAsync(cancellationToken);
        var lastActivityByProject = await GetLastResourcePlanActivityAsync([projectNumber], cancellationToken);
        var resolvedStatusName = project.StatusNumber.HasValue
            && statusNames.TryGetValue(project.StatusNumber.Value, out var statusName)
                ? statusName
                : null;

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
            StatusName = resolvedStatusName,
            LastResourcePlanActivityUtc = lastActivityByProject.TryGetValue(projectNumber, out var lastTouched)
                ? lastTouched
                : null,
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
            ProbabilityPercent = meta?.ProbabilityPercent ?? PlanningDefaults.ProbabilityPercent,
            LastPlanningReviewBy = meta?.LastPlanningReviewBy,
            Priority = meta?.Priority,
            IsBillableForPlanning = meta?.IsBillableForPlanning ?? false,
            IsAbsence = meta?.IsAbsence ?? false,
            IsInternal = meta?.IsInternal ?? false,
            IsProbableCase = meta?.IsProbableCase ?? false,
            // Absence of a metadata row means "not configured yet", not "hidden" — and
            // 6000+ synced projects have no row. Defaulting to false made every one of
            // them read as Nej, and because the admin UI loads this value into its draft
            // and saves it straight back, it also wrote that false into any metadata row
            // that was later created. Projects are plannable unless explicitly excluded.
            IsVisibleInPlanner = meta?.IsVisibleInPlanner ?? true,
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
            ProjectOwnerEmployeeNumber = meta?.ProjectOwnerEmployeeNumber,
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
            EngineeringDisciplineIds = disciplineIds,
            EngineeringDisciplines = disciplineNames,
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