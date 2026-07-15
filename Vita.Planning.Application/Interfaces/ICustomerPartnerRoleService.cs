using Vita.Planning.Application.DTOs;

namespace Vita.Planning.Application.Interfaces;

public interface ICustomerPartnerRoleService
{
    Task<IReadOnlyList<CustomerPartnerRoleDto>> GetByPlanningTargetIdAsync(int planningTargetId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CustomerPartnerRoleDto>> UpsertAsync(int planningTargetId, UpsertCustomerPartnerRolesRequest request, CancellationToken cancellationToken = default);
}
