using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Vita.Planning.Application.DTOs;
using Vita.Planning.Application.Interfaces;
using Vita.Planning.Infrastructure.Data;
using Vita.Planning.Infrastructure.Data.Entities;

namespace Vita.Planning.Api.Controllers;

[ApiController]
[Route("api/projects")]
public sealed class ProjectsController : ControllerBase
{
    private readonly IProjectQueryService _queryService;
    private readonly IProjectManagementService _managementService;
    private readonly IProjectWorkspaceProvisioningClient _workspaceProvisioningClient;
    private readonly PlanningDbContext _dbContext;

    public ProjectsController(
        IProjectQueryService queryService,
        IProjectManagementService managementService,
        IProjectWorkspaceProvisioningClient workspaceProvisioningClient,
        PlanningDbContext dbContext)
    {
        _queryService = queryService;
        _managementService = managementService;
        _workspaceProvisioningClient = workspaceProvisioningClient;
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<IActionResult> GetProjects(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 100,
        [FromQuery] string? query = null,
        CancellationToken cancellationToken = default)
    {
        var projects = await _queryService.GetProjectsAsync(page, pageSize, query, cancellationToken);
        return Ok(projects);
    }

    [HttpGet("{projectNumber:int}")]
    public async Task<IActionResult> GetProject(int projectNumber, CancellationToken cancellationToken)
    {
        var project = await _queryService.GetProjectByNumberAsync(projectNumber, cancellationToken);

        if (project is null)
            return NotFound();

        return Ok(project);
    }

    [HttpPost]
    public async Task<IActionResult> CreateProject(
        [FromBody] CreateProjectRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var caller = CallerInfo.FromClaimsPrincipal(User);
            request.CreatedBy ??= caller.UserId ?? caller.Email ?? caller.Name;
            request.OwnerUpn ??= caller.Email;
            var result = await _managementService.CreateProjectAsync(request, cancellationToken);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Debug/test endpoint: exercises workspace provisioning (M365 group, Teams, SharePoint,
    /// Outlook folder) in isolation, without touching e-conomic or the database. Use a fake
    /// MainProjectNumber (e.g. 26999999) to target a given year's mailbox without creating a
    /// real project.
    /// </summary>
    [HttpPost("test-workspace-provisioning")]
    public async Task<IActionResult> TestWorkspaceProvisioning(
        [FromBody] ProjectWorkspaceProvisioningRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _workspaceProvisioningClient.ProvisionAsync(request, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("{projectNumber:int}/activities")]
    public async Task<ActionResult<IReadOnlyList<ActivityDto>>> GetActivities(
        int projectNumber,
        CancellationToken cancellationToken)
    {
        var activities = await _dbContext.ProjectActivities
            .AsNoTracking()
            .Where(pa => pa.ProjectNumber == projectNumber)
            .Join(
                _dbContext.Activities,
                pa => pa.ActivityNumber,
                a => a.ActivityNumber,
                (pa, a) => new ActivityDto
                {
                    ProjectActivityId = pa.Number,
                    ActivityNumber = a.ActivityNumber,
                    ActivityGroupNumber = a.ActivityGroupNumber,
                    Name = a.Name,
                    CostPriceMarkupPercentage = a.CostPriceMarkupPercentage,
                    CutoffDate = a.CutoffDate,
                    HideInSearch = a.HideInSearch,
                    InLieuCode = a.InLieuCode,
                    IsAccessible = a.IsAccessible,
                    SalesPriceAfter = a.SalesPriceAfter,
                    SalesPriceBefore = a.SalesPriceBefore,
                    ObjectVersion = a.ObjectVersion,
                    SourceLastSyncedAt = a.SourceLastSyncedAt
                })
            .OrderBy(a => a.Name)
            .ThenBy(a => a.ActivityNumber)
            .ToListAsync(cancellationToken);

        return Ok(activities);
    }

    [HttpGet("{projectNumber:int}/team")]
    public async Task<ActionResult<IReadOnlyList<ProjectTeamMemberDto>>> GetTeam(
        int projectNumber,
        CancellationToken cancellationToken)
    {
        var metaId = await _dbContext.ProjectMetadata
            .AsNoTracking()
            .Where(m => m.ProjectNumber == projectNumber)
            .Select(m => (int?)m.ProjectMetadataId)
            .FirstOrDefaultAsync(cancellationToken);

        if (metaId is null)
            return Ok(Array.Empty<ProjectTeamMemberDto>());

        var members = await (
            from tm in _dbContext.ProjectTeamMembers.AsNoTracking()
                .Where(x => x.ProjectMetadataId == metaId.Value)
            from user in _dbContext.Users.AsNoTracking()
                .Where(u => u.EmployeeId == tm.EmployeeId)
                .DefaultIfEmpty()
            from contact in _dbContext.CompanyContacts.AsNoTracking()
                .Where(c => c.CompanyContactId == tm.CompanyContactId)
                .DefaultIfEmpty()
            from customer in _dbContext.Customers.AsNoTracking()
                .Where(c => c.CustomerId == contact.CustomerId)
                .DefaultIfEmpty()
            from discipline in _dbContext.EngineeringDisciplines.AsNoTracking()
                .Where(d => d.EngineeringDisciplineId == tm.EngineeringDisciplineId)
                .DefaultIfEmpty()
            orderby tm.SortOrder, tm.ProjectTeamMemberId
            select new ProjectTeamMemberDto
            {
                ProjectTeamMemberId = tm.ProjectTeamMemberId,
                MemberType = tm.MemberType,
                EmployeeId = tm.EmployeeId,
                DisplayName = user == null ? null : user.DisplayName,
                OfficeLocation = user == null ? null : user.OfficeLocation,
                Department = user == null ? null : user.Department,
                CompanyContactId = tm.CompanyContactId,
                ContactName = contact == null ? null : contact.Name,
                CompanyName = customer == null ? null : customer.Name,
                Email = contact == null ? null : contact.Email,
                RoleDescription = tm.RoleDescription,
                EngineeringDisciplineId = tm.EngineeringDisciplineId,
                EngineeringDisciplineName = discipline == null ? null : discipline.Name,
                SortOrder = tm.SortOrder
            }
        ).ToListAsync(cancellationToken);

        return Ok(members);
    }

    [HttpPost("{projectNumber:int}/team")]
    public async Task<ActionResult<ProjectTeamMemberDto>> AddTeamMember(
        int projectNumber,
        [FromBody] AddProjectTeamMemberRequest request,
        CancellationToken cancellationToken)
    {
        var metaId = await _dbContext.ProjectMetadata
            .AsNoTracking()
            .Where(m => m.ProjectNumber == projectNumber)
            .Select(m => (int?)m.ProjectMetadataId)
            .FirstOrDefaultAsync(cancellationToken);

        if (metaId is null)
            return NotFound();

        if (request.MemberType == "Internal" && request.EmployeeId is null)
            return BadRequest(new { message = "EmployeeId is required for Internal members." });

        if (request.MemberType == "External" && request.CompanyContactId is null)
            return BadRequest(new { message = "CompanyContactId is required for External members." });

        if (request.MemberType is not "Internal" and not "External")
            return BadRequest(new { message = "MemberType must be 'Internal' or 'External'." });

        var entity = new ProjectTeamMember
        {
            ProjectMetadataId = metaId.Value,
            MemberType = request.MemberType,
            EmployeeId = request.MemberType == "Internal" ? request.EmployeeId : null,
            CompanyContactId = request.MemberType == "External" ? request.CompanyContactId : null,
            RoleDescription = request.RoleDescription,
            EngineeringDisciplineId = request.EngineeringDisciplineId,
            SortOrder = request.SortOrder,
            CreatedAtUtc = DateTime.UtcNow
        };

        _dbContext.ProjectTeamMembers.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(
            nameof(GetTeam),
            new { projectNumber },
            await BuildMemberDtoAsync(entity, cancellationToken));
    }

    [HttpPut("{projectNumber:int}/team/{memberId:int}")]
    public async Task<ActionResult<ProjectTeamMemberDto>> UpdateTeamMember(
        int projectNumber,
        int memberId,
        [FromBody] UpdateProjectTeamMemberRequest request,
        CancellationToken cancellationToken)
    {
        var metaId = await _dbContext.ProjectMetadata
            .AsNoTracking()
            .Where(m => m.ProjectNumber == projectNumber)
            .Select(m => (int?)m.ProjectMetadataId)
            .FirstOrDefaultAsync(cancellationToken);

        if (metaId is null)
            return NotFound();

        var entity = await _dbContext.ProjectTeamMembers
            .Where(x => x.ProjectTeamMemberId == memberId && x.ProjectMetadataId == metaId.Value)
            .FirstOrDefaultAsync(cancellationToken);

        if (entity is null)
            return NotFound();

        entity.RoleDescription = request.RoleDescription;
        entity.EngineeringDisciplineId = request.EngineeringDisciplineId;
        entity.SortOrder = request.SortOrder;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(await BuildMemberDtoAsync(entity, cancellationToken));
    }

    [HttpDelete("{projectNumber:int}/team/{memberId:int}")]
    public async Task<IActionResult> RemoveTeamMember(
        int projectNumber,
        int memberId,
        CancellationToken cancellationToken)
    {
        var metaId = await _dbContext.ProjectMetadata
            .AsNoTracking()
            .Where(m => m.ProjectNumber == projectNumber)
            .Select(m => (int?)m.ProjectMetadataId)
            .FirstOrDefaultAsync(cancellationToken);

        if (metaId is null)
            return NotFound();

        var entity = await _dbContext.ProjectTeamMembers
            .Where(x => x.ProjectTeamMemberId == memberId && x.ProjectMetadataId == metaId.Value)
            .FirstOrDefaultAsync(cancellationToken);

        if (entity is null)
            return NotFound();

        _dbContext.ProjectTeamMembers.Remove(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    private async Task<ProjectTeamMemberDto> BuildMemberDtoAsync(
        ProjectTeamMember entity,
        CancellationToken cancellationToken)
    {
        string? displayName = null, officeLocation = null, department = null;
        if (entity.EmployeeId.HasValue)
        {
            var user = await _dbContext.Users.AsNoTracking()
                .Where(u => u.EmployeeId == entity.EmployeeId.Value)
                .Select(u => new { u.DisplayName, u.OfficeLocation, u.Department })
                .FirstOrDefaultAsync(cancellationToken);
            displayName = user?.DisplayName;
            officeLocation = user?.OfficeLocation;
            department = user?.Department;
        }

        string? contactName = null, companyName = null, email = null;
        if (entity.CompanyContactId.HasValue)
        {
            var contact = await _dbContext.CompanyContacts.AsNoTracking()
                .Where(c => c.CompanyContactId == entity.CompanyContactId.Value)
                .Select(c => new { c.Name, c.Email, c.CustomerId })
                .FirstOrDefaultAsync(cancellationToken);
            if (contact is not null)
            {
                contactName = contact.Name;
                email = contact.Email;
                companyName = await _dbContext.Customers.AsNoTracking()
                    .Where(c => c.CustomerId == contact.CustomerId)
                    .Select(c => c.Name)
                    .FirstOrDefaultAsync(cancellationToken);
            }
        }

        string? disciplineName = null;
        if (entity.EngineeringDisciplineId.HasValue)
        {
            disciplineName = await _dbContext.EngineeringDisciplines.AsNoTracking()
                .Where(d => d.EngineeringDisciplineId == entity.EngineeringDisciplineId.Value)
                .Select(d => d.Name)
                .FirstOrDefaultAsync(cancellationToken);
        }

        return new ProjectTeamMemberDto
        {
            ProjectTeamMemberId = entity.ProjectTeamMemberId,
            MemberType = entity.MemberType,
            EmployeeId = entity.EmployeeId,
            DisplayName = displayName,
            OfficeLocation = officeLocation,
            Department = department,
            CompanyContactId = entity.CompanyContactId,
            ContactName = contactName,
            CompanyName = companyName,
            Email = email,
            RoleDescription = entity.RoleDescription,
            EngineeringDisciplineId = entity.EngineeringDisciplineId,
            EngineeringDisciplineName = disciplineName,
            SortOrder = entity.SortOrder
        };
    }

    [HttpPut("{projectNumber:int}/partners")]
    public async Task<IActionResult> UpdatePartners(
        int projectNumber,
        [FromBody] IReadOnlyList<ProjectPartnerRequest> partners,
        CancellationToken cancellationToken)
    {
        try
        {
            var caller = CallerInfo.FromClaimsPrincipal(User);
            var calledBy = caller.UserId ?? caller.Email ?? caller.Name;
            await _managementService.UpdateProjectPartnersAsync(projectNumber, partners, calledBy, cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}