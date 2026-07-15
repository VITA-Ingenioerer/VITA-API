using Microsoft.EntityFrameworkCore;
using Vita.Planning.Application.DTOs;
using Vita.Planning.Application.Interfaces;
using Vita.Planning.Infrastructure.Data;
using Vita.Planning.Infrastructure.Data.Entities;

namespace Vita.Planning.Infrastructure.Services;

public sealed class ProjectManagementService : IProjectManagementService
{
    private readonly PlanningDbContext _db;
    private readonly IEconomicProjectNumberAllocator _projectNumberAllocator;
    private readonly IEconomicProjectSourceClient _projectSourceClient;
    private readonly IProjectWorkspaceProvisioningClient _workspaceProvisioningClient;
    private readonly IEntityChangeLogService _changeLog;

    public ProjectManagementService(
        PlanningDbContext db,
        IEconomicProjectNumberAllocator projectNumberAllocator,
        IEconomicProjectSourceClient projectSourceClient,
        IProjectWorkspaceProvisioningClient workspaceProvisioningClient,
        IEntityChangeLogService changeLog)
    {
        _db = db;
        _projectNumberAllocator = projectNumberAllocator;
        _projectSourceClient = projectSourceClient;
        _workspaceProvisioningClient = workspaceProvisioningClient;
        _changeLog = changeLog;
    }

    private static CallerInfo ResolveCaller(string? name) =>
        string.IsNullOrWhiteSpace(name)
            ? CallerInfo.System
            : new CallerInfo { UserId = name, Name = name, Email = string.Empty };

    public async Task<CreateProjectResult> CreateProjectAsync(
        CreateProjectRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.SubProjectNames.Count > 99)
            throw new InvalidOperationException("A main project can have at most 99 sub-projects.");

        var customer = await _db.Customers
            .Where(c => c.CustomerId == request.CustomerId)
            .Select(c => new { c.Name, c.ExtCustomerNumber })
            .FirstOrDefaultAsync(cancellationToken);

        if (customer is null)
            throw new InvalidOperationException($"Customer {request.CustomerId} not found.");

        if (customer.ExtCustomerNumber is null)
            throw new InvalidOperationException(
                $"Customer '{customer.Name}' does not exist in e-conomic. " +
                "Create the customer in e-conomic first, then run a sync before creating this project.");

        var economicCustomerNumber = customer.ExtCustomerNumber.Value;
        var mainProjectNumber = await _projectNumberAllocator.CreateMainProjectAsync(
            name: request.Name,
            projectGroupNumber: request.ProjectGroupNumber,
            customerNumber: economicCustomerNumber,
            responsibleEmployeeNumber: request.ResponsibleEmployeeNumber,
            cancellationToken: cancellationToken);

        await UpsertProjectSyncStubAsync(mainProjectNumber, request.Name, isMainProject: true, cancellationToken);

        var createdAt = DateTime.UtcNow;
        var subProjectsCreated = new List<int>();
        var subProjectFailures = new List<SubProjectFailure>();

        for (var i = 0; i < request.SubProjectNames.Count; i++)
        {
            var subNumber = mainProjectNumber + i + 1;
            var subName = $"{request.Name} - {request.SubProjectNames[i]}";

            if (subProjectFailures.Count > 0)
            {
                subProjectFailures.Add(new SubProjectFailure
                {
                    SubProjectNumber = subNumber,
                    SubProjectName = subName,
                    WasAttempted = false,
                    Error = "Not attempted due to earlier sub-project failure. Create manually in e-conomic."
                });
                continue;
            }

            try
            {
                var actualSubNumber = await _projectNumberAllocator.CreateSubProjectAsync(
                    mainProjectNumber: mainProjectNumber,
                    preferredOffset: i + 1,
                    name: subName,
                    projectGroupNumber: request.ProjectGroupNumber,
                    customerNumber: economicCustomerNumber,
                    responsibleEmployeeNumber: request.ResponsibleEmployeeNumber,
                    cancellationToken: cancellationToken);

                await UpsertProjectSyncStubAsync(actualSubNumber, subName, isMainProject: false, cancellationToken);

                subProjectsCreated.Add(actualSubNumber);
            }
            catch (InvalidOperationException ex)
            {
                subProjectFailures.Add(new SubProjectFailure
                {
                    SubProjectNumber = subNumber,
                    SubProjectName = subName,
                    WasAttempted = true,
                    Error = ex.Message
                });
            }
        }

        ProjectWorkspaceProvisioningResult? workspace = null;
        if (!request.SkipWorkspaceProvisioning)
        {
            try
            {
                var ownerUpn = request.OwnerUpn;
                if (string.IsNullOrWhiteSpace(ownerUpn))
                {
                    ownerUpn = await _db.Users
                        .Where(u => u.EmployeeId == 122)
                        .Select(u => u.UserPrincipalName)
                        .FirstOrDefaultAsync(cancellationToken);
                }

                workspace = await _workspaceProvisioningClient.ProvisionAsync(
                    new ProjectWorkspaceProvisioningRequest
                    {
                        MainProjectNumber = mainProjectNumber,
                        ProjectName = request.Name,
                        IsPrivate = request.IsPrivate,
                        MemberUserIds = request.MemberUserIds,
                        CreateTeam = request.CreateTeam,
                        OwnerAadIdentifier = ownerUpn,
                        InitiatedBy = request.CreatedBy
                    },
                    cancellationToken);
            }
            catch (Exception ex)
            {
                workspace = new ProjectWorkspaceProvisioningResult
                {
                    Warnings = [$"Workspace provisioning failed: {ex.Message}"]
                };
            }
        }

        await ApplyWorkspaceLinksToMetadataAsync(mainProjectNumber, workspace, cancellationToken);

        var planningTarget = await FindOrCreateProjectPlanningTargetAsync(mainProjectNumber, request.Name, cancellationToken);
        if (request.Partners.Count > 0)
            await SaveProjectPartnersAsync(planningTarget.PlanningTargetId, request.Partners, request.CreatedBy, cancellationToken);

        await _changeLog.RecordChangeAsync(new RecordEntityChangeRequest
        {
            EventType = "ProjectCreated",
            EventTitle = $"Projekt oprettet: {mainProjectNumber} {request.Name}",
            EntityType = "Project",
            EntityId = mainProjectNumber.ToString(),
            PlanningTargetId = planningTarget.PlanningTargetId,
            NewValue = request.Name,
            NewSnapshot = new { mainProjectNumber, request.Name, subProjectsCreated, request.CustomerId },
            ChangeReason = "CreateProject",
            Caller = ResolveCaller(request.CreatedBy),
            SourceModule = "ProjectManagementService"
        }, cancellationToken);

        return new CreateProjectResult
        {
            MainProjectNumber = mainProjectNumber,
            ProjectName = request.Name,
            CreatedAtUtc = createdAt,
            SubProjectsCreated = subProjectsCreated,
            SubProjectFailures = subProjectFailures,
            Workspace = workspace
        };
    }

    public async Task UpdateProjectPartnersAsync(
        int projectNumber,
        IReadOnlyList<ProjectPartnerRequest> partners,
        string? updatedBy = null,
        CancellationToken cancellationToken = default)
    {
        var projectName = await _db.Projects
            .Where(p => p.ProjectNumber == projectNumber)
            .Select(p => p.ProjectName)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException($"Project {projectNumber} not found.");

        var target = await FindOrCreateProjectPlanningTargetAsync(projectNumber, projectName, cancellationToken);
        await SaveProjectPartnersAsync(target.PlanningTargetId, partners, updatedBy, cancellationToken);
    }

    private async Task ApplyWorkspaceLinksToMetadataAsync(
        int projectNumber,
        ProjectWorkspaceProvisioningResult? workspace,
        CancellationToken cancellationToken)
    {
        if (workspace is null)
            return;

        var hasAnyLink = workspace.GroupId is not null
            || workspace.SharePointSiteUrl is not null
            || workspace.SiteId is not null
            || workspace.DriveId is not null
            || workspace.OutlookFolderId is not null;

        if (!hasAnyLink)
            return;

        // Find the metadata row that belongs to this project (may already exist if the frontend
        // saved metadata before/around the same time) and update only the workspace/archive
        // fields on it — never overwrite the rest of the row.
        var metadata = await _db.ProjectMetadata
            .FirstOrDefaultAsync(m => m.ProjectNumber == projectNumber, cancellationToken);

        if (metadata is null)
        {
            metadata = new ProjectMetadata
            {
                ProjectNumber = projectNumber,
                CreatedAtUtc = DateTime.UtcNow
            };
            _db.ProjectMetadata.Add(metadata);
        }
        else
        {
            metadata.UpdatedAtUtc = DateTime.UtcNow;
        }

        metadata.ProjectArchiveGroupId = workspace.GroupId ?? metadata.ProjectArchiveGroupId;
        metadata.ProjectArchiveUrl = workspace.SharePointSiteUrl ?? metadata.ProjectArchiveUrl;
        metadata.ProjectArchiveSiteId = workspace.SiteId ?? metadata.ProjectArchiveSiteId;
        metadata.ProjectArchiveDriveId = workspace.DriveId ?? metadata.ProjectArchiveDriveId;
        metadata.ProjectArchiveOutlookFolderId = workspace.OutlookFolderId ?? metadata.ProjectArchiveOutlookFolderId;

        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task UpsertProjectSyncStubAsync(
        int projectNumber,
        string fallbackName,
        bool isMainProject,
        CancellationToken cancellationToken)
    {
        // Insert the sync mirror row immediately after creating the project in e-conomic, so the
        // FK on core.planning_targets is satisfied right away instead of waiting for the next
        // scheduled sync. We GET the project back from e-conomic rather than guessing at its
        // fields from our own create request, so this row reflects what e-conomic actually stored.
        var stubExists = await _db.Projects
            .AnyAsync(p => p.ProjectNumber == projectNumber, cancellationToken);

        if (stubExists)
            return;

        var source = await _projectSourceClient.GetProjectByNumberAsync(projectNumber, cancellationToken);

        if (source is not null)
        {
            _db.Projects.Add(ExtProjectMapper.ToNewEntity(source));
        }
        else
        {
            // e-conomic didn't return the project on an immediate read-after-write (rare) —
            // fall back to a minimal row so the FK insert succeeds; the next scheduled sync
            // will fill in the real data.
            _db.Projects.Add(new ExtProject
            {
                ProjectNumber = projectNumber,
                ProjectName = fallbackName,
                IsMainProject = isMainProject,
                SourceLastSyncedAt = DateTime.UtcNow
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task<PlanningTarget> FindOrCreateProjectPlanningTargetAsync(
        int projectNumber,
        string projectName,
        CancellationToken cancellationToken)
    {
        var target = await _db.PlanningTargets
            .FirstOrDefaultAsync(pt => pt.ExtProjectNumber == projectNumber, cancellationToken);

        if (target is not null)
            return target;

        target = new PlanningTarget
        {
            Code = projectNumber.ToString(),
            Name = projectName,
            TargetType = "BillableProject",
            ExtProjectNumber = projectNumber,
            IsActive = true,
            IsPlannable = true,
            CreatedAtUtc = DateTime.UtcNow
        };
        _db.PlanningTargets.Add(target);
        await _db.SaveChangesAsync(cancellationToken);
        return target;
    }

    private async Task SaveProjectPartnersAsync(
        int planningTargetId,
        IEnumerable<ProjectPartnerRequest> partners,
        string? savedBy,
        CancellationToken cancellationToken)
    {
        var existing = await _db.CustomerPartnerRoles
            .Where(r => r.PlanningTargetId == planningTargetId)
            .ToListAsync(cancellationToken);

        _db.CustomerPartnerRoles.RemoveRange(existing);

        var now = DateTime.UtcNow;
        foreach (var p in partners)
        {
            _db.CustomerPartnerRoles.Add(new CustomerPartnerRole
            {
                PlanningTargetId = planningTargetId,
                CustomerId = p.CustomerId,
                PlanningPartnerRoleTypeId = p.RoleTypeId,
                CompanyContactId = p.CustomerContactId,
                CreatedBy = savedBy,
                CreatedAtUtc = now
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
    }
}
