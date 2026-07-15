using Vita.Planning.Application.DTOs;

namespace Vita.Planning.Application.Interfaces;

public interface ILookupService
{
    Task<IReadOnlyList<LookupItemDto>> GetOfferStatusesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LookupItemDto>> GetPlanningPartnerRoleTypesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LookupItemDto>> GetCompetitionFormsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LookupItemDto>> GetEnterpriseFormsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LookupItemDto>> GetConsultantFormsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LookupItemDto>> GetProjectTypesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LookupItemDto>> GetProjectRolesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LookupItemDto>> GetComplexityLevelsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LookupItemDto>> GetEngineeringDisciplinesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SegmentDto>> GetSegmentsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<VirtualResourceDto>> GetVirtualResourcesAsync(CancellationToken cancellationToken = default);
}
