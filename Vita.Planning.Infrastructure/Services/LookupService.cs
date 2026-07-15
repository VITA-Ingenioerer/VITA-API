using Microsoft.EntityFrameworkCore;
using Vita.Planning.Application.DTOs;
using Vita.Planning.Application.Interfaces;
using Vita.Planning.Infrastructure.Data;

namespace Vita.Planning.Infrastructure.Services;

public sealed class LookupService : ILookupService
{
    private readonly PlanningDbContext _dbContext;

    public LookupService(PlanningDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<LookupItemDto>> GetPlanningPartnerRoleTypesAsync(CancellationToken cancellationToken = default) =>
        await _dbContext.PlanningPartnerRoleTypes
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.SortOrder).ThenBy(x => x.Name)
            .Select(x => new LookupItemDto { Id = x.PlanningPartnerRoleTypeId, Code = x.Code, Name = x.Name, SortOrder = x.SortOrder })
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<LookupItemDto>> GetOfferStatusesAsync(CancellationToken cancellationToken = default) =>
        await _dbContext.OfferStatuses
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.SortOrder).ThenBy(x => x.Name)
            .Select(x => new LookupItemDto { Id = x.OfferStatusId, Code = x.Code, Name = x.Name, SortOrder = x.SortOrder })
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<LookupItemDto>> GetCompetitionFormsAsync(CancellationToken cancellationToken = default) =>
        await _dbContext.CompetitionForms
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.SortOrder).ThenBy(x => x.Name)
            .Select(x => new LookupItemDto { Id = x.CompetitionFormId, Code = x.Code, Name = x.Name, SortOrder = x.SortOrder })
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<LookupItemDto>> GetEnterpriseFormsAsync(CancellationToken cancellationToken = default) =>
        await _dbContext.EnterpriseForms
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.SortOrder).ThenBy(x => x.Name)
            .Select(x => new LookupItemDto { Id = x.EnterpriseFormId, Code = x.Code, Name = x.Name, SortOrder = x.SortOrder })
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<LookupItemDto>> GetConsultantFormsAsync(CancellationToken cancellationToken = default) =>
        await _dbContext.ConsultantForms
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.SortOrder).ThenBy(x => x.Name)
            .Select(x => new LookupItemDto { Id = x.ConsultantFormId, Code = x.Code, Name = x.Name, SortOrder = x.SortOrder })
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<LookupItemDto>> GetProjectTypesAsync(CancellationToken cancellationToken = default) =>
        await _dbContext.ProjectTypes
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.SortOrder).ThenBy(x => x.Name)
            .Select(x => new LookupItemDto { Id = x.ProjectTypeId, Code = x.Code, Name = x.Name, SortOrder = x.SortOrder })
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<LookupItemDto>> GetProjectRolesAsync(CancellationToken cancellationToken = default) =>
        await _dbContext.ProjectRoles
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.SortOrder).ThenBy(x => x.Name)
            .Select(x => new LookupItemDto { Id = x.ProjectRoleId, Code = x.Code, Name = x.Name, SortOrder = x.SortOrder })
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<LookupItemDto>> GetComplexityLevelsAsync(CancellationToken cancellationToken = default) =>
        await _dbContext.ComplexityLevels
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.SortOrder).ThenBy(x => x.Name)
            .Select(x => new LookupItemDto { Id = x.ComplexityLevelId, Code = x.Code, Name = x.Name, SortOrder = x.SortOrder })
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<LookupItemDto>> GetEngineeringDisciplinesAsync(CancellationToken cancellationToken = default) =>
        await _dbContext.EngineeringDisciplines
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.SortOrder).ThenBy(x => x.Name)
            .Select(x => new LookupItemDto { Id = x.EngineeringDisciplineId, Code = x.Code, Name = x.Name, SortOrder = x.SortOrder })
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<SegmentDto>> GetSegmentsAsync(CancellationToken cancellationToken = default) =>
        await _dbContext.Segments
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.SortOrder).ThenBy(x => x.Name)
            .Select(x => new SegmentDto { SegmentId = x.SegmentId, Name = x.Name, SortOrder = x.SortOrder })
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<VirtualResourceDto>> GetVirtualResourcesAsync(CancellationToken cancellationToken = default) =>
        await _dbContext.VirtualResources
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.Name)
            .Select(x => new VirtualResourceDto
            {
                VirtualResourceId = x.VirtualResourceId,
                Code = x.Code,
                Name = x.Name,
                DisciplineId = x.DisciplineId
            })
            .ToListAsync(cancellationToken);
}
