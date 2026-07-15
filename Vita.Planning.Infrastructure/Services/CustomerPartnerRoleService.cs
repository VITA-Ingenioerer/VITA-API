using Microsoft.EntityFrameworkCore;
using Vita.Planning.Application.DTOs;
using Vita.Planning.Application.Interfaces;
using Vita.Planning.Infrastructure.Data;
using Vita.Planning.Infrastructure.Data.Entities;

namespace Vita.Planning.Infrastructure.Services;

public sealed class CustomerPartnerRoleService : ICustomerPartnerRoleService
{
    private readonly PlanningDbContext _dbContext;

    public CustomerPartnerRoleService(PlanningDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<CustomerPartnerRoleDto>> GetByPlanningTargetIdAsync(
        int planningTargetId,
        CancellationToken cancellationToken = default)
    {
        return await (
            from r in _dbContext.CustomerPartnerRoles.AsNoTracking()
                .Where(x => x.PlanningTargetId == planningTargetId)
            join c in _dbContext.Customers on r.CustomerId equals c.CustomerId
            join rt in _dbContext.PlanningPartnerRoleTypes on r.PlanningPartnerRoleTypeId equals rt.PlanningPartnerRoleTypeId
            from cc in _dbContext.CompanyContacts
                .Where(x => x.CompanyContactId == r.CompanyContactId)
                .DefaultIfEmpty()
            orderby r.IsPrimary ? 0 : 1, rt.Name
            select new CustomerPartnerRoleDto
            {
                CustomerPartnerRoleId = r.CustomerPartnerRoleId,
                PlanningTargetId = r.PlanningTargetId,
                CustomerId = r.CustomerId,
                CustomerName = c.Name,
                CvrNumber = c.CvrNumber,
                PlanningPartnerRoleTypeId = rt.PlanningPartnerRoleTypeId,
                RoleTypeCode = rt.Code,
                RoleTypeName = rt.Name,
                IsPrimary = r.IsPrimary,
                CustomerContactId = r.CompanyContactId,
                ContactPersonName = cc == null ? null : cc.Name,
                ContactPersonEmail = cc == null ? null : cc.Email,
                ContactPersonPhone = cc == null ? null : cc.Phone
            }
        ).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CustomerPartnerRoleDto>> UpsertAsync(
        int planningTargetId,
        UpsertCustomerPartnerRolesRequest request,
        CancellationToken cancellationToken = default)
    {
        var roleTypeIds = request.Customers
            .Select(x => x.PlanningPartnerRoleTypeId)
            .Distinct()
            .ToList();

        var validRoleTypes = await _dbContext.PlanningPartnerRoleTypes
            .Where(x => roleTypeIds.Contains(x.PlanningPartnerRoleTypeId) && x.IsActive)
            .ToDictionaryAsync(x => x.PlanningPartnerRoleTypeId, cancellationToken);

        foreach (var item in request.Customers)
        {
            if (!validRoleTypes.ContainsKey(item.PlanningPartnerRoleTypeId))
                throw new InvalidOperationException(
                    $"PlanningPartnerRoleType with id {item.PlanningPartnerRoleTypeId} does not exist or is inactive.");
        }

        var existing = await _dbContext.CustomerPartnerRoles
            .Where(x => x.PlanningTargetId == planningTargetId)
            .ToListAsync(cancellationToken);

        _dbContext.CustomerPartnerRoles.RemoveRange(existing);

        foreach (var item in request.Customers)
        {
            var roleType = validRoleTypes[item.PlanningPartnerRoleTypeId];

            _dbContext.CustomerPartnerRoles.Add(new CustomerPartnerRole
            {
                PlanningTargetId = planningTargetId,
                CustomerId = item.CustomerId,
                PlanningPartnerRoleTypeId = item.PlanningPartnerRoleTypeId,
                RoleType = roleType.Name,
                IsPrimary = item.IsPrimary,
                CompanyContactId = item.CustomerContactId,
                CreatedAtUtc = DateTime.UtcNow
            });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return await GetByPlanningTargetIdAsync(planningTargetId, cancellationToken);
    }

}
