using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Web;
using Vita.Atlas.Application.DTOs;
using Vita.Atlas.Application.Interfaces;
using Vita.Atlas.Infrastructure.Clients;
using Vita.Atlas.Infrastructure.Data;
using Vita.Atlas.Infrastructure.Services;
using Vita.Atlas.Api.HostedServices;
using Vita.Atlas.Api.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAd"));

var requireGlobalAuthentication =
    builder.Configuration.GetValue<bool>(
        "Authentication:RequireGlobalAuthentication");

// Employee classification drives Entra dynamic group membership and potentially
// downstream access, so writing it is deliberately narrower than ordinary planner access.
// A caller qualifies by holding the app role, or by being on an explicit UPN allowlist —
// the allowlist exists so the feature can be operated before app roles are rolled out.
// Both default to empty/unassigned, i.e. nobody can write until one is configured.
var classificationWriteRole =
    builder.Configuration["EntraClassification:WriteRole"]
    ?? "Planning.EmployeeClassification.Write";

var classificationWriteUpns =
    builder.Configuration.GetSection("EntraClassification:WriteUserPrincipalNames").Get<string[]>()
    ?? [];

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("PlannerAccess", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireScope("Planner.Access");
    });

    options.AddPolicy("EmployeeClassificationWrite", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireScope("Planner.Access");
        policy.RequireAssertion(context =>
        {
            // Checked as a raw claim rather than via IsInRole: app roles arrive in the
            // "roles" claim, which is not the role claim type JwtBearer maps by default.
            var hasRole = context.User.Claims.Any(c =>
                (c.Type == "roles" || c.Type == System.Security.Claims.ClaimTypes.Role) &&
                string.Equals(c.Value, classificationWriteRole, StringComparison.OrdinalIgnoreCase));

            if (hasRole)
            {
                return true;
            }

            var upn = context.User.FindFirst("upn")?.Value
                      ?? context.User.FindFirst("preferred_username")?.Value;

            return !string.IsNullOrWhiteSpace(upn) &&
                   classificationWriteUpns.Contains(upn, StringComparer.OrdinalIgnoreCase);
        });
    });

    if (requireGlobalAuthentication)
    {
        options.FallbackPolicy =
            new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .RequireScope("Planner.Access")
                .Build();
    }
});
var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? [];

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        policy
            .WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

builder.Services.AddMemoryCache();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICorrelationContext, HttpCorrelationContext>();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks();

var connectionString = builder.Configuration.GetConnectionString("AtlasDatabase")
                      ?? throw new InvalidOperationException("Connection string 'AtlasDatabase' is missing.");

builder.Services.AddDbContext<AtlasDbContext>(options =>
    options.UseSqlServer(connectionString, sql =>
    {
        sql.EnableRetryOnFailure();
    }));

builder.Services.AddScoped<IInternalUserSourceClient, MergedEconomicGraphUserSourceClient>();

builder.Services.AddHttpClient("NagerDate", client =>
{
    client.BaseAddress = new Uri("https://date.nager.at/");
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.Configure<EconomicSettings>(
    builder.Configuration.GetSection("Economic"));

builder.Services.Configure<VirkSettings>(
    builder.Configuration.GetSection("Virk"));

builder.Services.Configure<OutlookTilbudssagerSettings>(
    builder.Configuration.GetSection("OutlookTilbudssager"));


builder.Services.AddHttpClient<IEconomicProjectSourceClient, EconomicProjectSourceClient>((sp, client) =>
{
    var settings = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<EconomicSettings>>().Value;
    client.BaseAddress = new Uri(settings.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(60);
});

builder.Services.AddHttpClient<IEconomicProjectGroupSourceClient, EconomicProjectGroupSourceClient>((sp, client) =>
{
    var settings = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<EconomicSettings>>().Value;
    client.BaseAddress = new Uri(settings.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(60);
});

builder.Services.AddHttpClient<IEconomicProjectCustomerSourceClient, EconomicProjectCustomerSourceClient>((sp, client) =>
{
    var settings = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<EconomicSettings>>().Value;
    client.BaseAddress = new Uri(settings.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(60);
});

builder.Services.AddHttpClient<IEconomicProjectStatusSourceClient, EconomicProjectStatusSourceClient>((sp, client) =>
{
    var settings = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<EconomicSettings>>().Value;
    client.BaseAddress = new Uri(settings.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(60);
});

builder.Services.AddHttpClient<IEconomicProjectEmployeeGroupSourceClient, EconomicProjectEmployeeGroupSourceClient>((sp, client) =>
{
    var settings = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<EconomicSettings>>().Value;
    client.BaseAddress = new Uri(settings.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(60);
});

builder.Services.AddHttpClient<IEconomicProjectEmployeeSourceClient, EconomicProjectEmployeeSourceClient>((sp, client) =>
{
    var settings = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<EconomicSettings>>().Value;
    client.BaseAddress = new Uri(settings.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(60);
});

builder.Services.AddHttpClient<IEconomicActivitySourceClient, EconomicActivitySourceClient>((sp, client) =>
{
    var settings = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<EconomicSettings>>().Value;
    client.BaseAddress = new Uri(settings.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(60);
});

builder.Services.AddHttpClient<IEconomicProjectActivitySourceClient, EconomicProjectActivitySourceClient>((sp, client) =>
{
    var settings = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<EconomicSettings>>().Value;
    client.BaseAddress = new Uri(settings.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(60);
});

builder.Services.AddHttpClient<IOutlookTilbudssagerClient, OutlookTilbudssagerClient>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(60);
});

builder.Services.AddHttpClient<IEconomicTimeEntryClient, EconomicTimeEntryClient>((sp, client) =>
{
    var settings = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<EconomicSettings>>().Value;
    client.BaseAddress = new Uri(settings.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(60);
});

builder.Services.AddHttpClient<IEconomicProjectWriteClient, EconomicProjectWriteClient>((sp, client) =>
{
    var settings = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<EconomicSettings>>().Value;
    client.BaseAddress = new Uri(settings.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(60);
});
builder.Services.Configure<MicrosoftGraphSettings>(
    builder.Configuration.GetSection("MicrosoftGraph"));

builder.Services.Configure<EntraClassificationSettings>(
    builder.Configuration.GetSection("EntraClassification"));

// Employee classification talks to the same app registration as the other Graph clients.
// Rather than making the same tenant/client/secret be configured twice, anything left
// unset in the EntraClassification section inherits from MicrosoftGraph. Setting a value
// explicitly still wins, so the two can be split later without a code change.
builder.Services.PostConfigure<EntraClassificationSettings>(settings =>
{
    var graph = builder.Configuration.GetSection("MicrosoftGraph");

    if (string.IsNullOrWhiteSpace(settings.TenantId))
    {
        settings.TenantId = graph["TenantId"] ?? string.Empty;
    }

    if (string.IsNullOrWhiteSpace(settings.ClientId))
    {
        settings.ClientId = graph["ClientId"] ?? string.Empty;
    }

    if (string.IsNullOrWhiteSpace(settings.ClientSecret))
    {
        settings.ClientSecret = graph["ClientSecret"] ?? string.Empty;
    }
});

builder.Services.Configure<TilbudssagerSettings>(
    builder.Configuration.GetSection("Tilbudssager"));

builder.Services.Configure<RessourceplanWorkbookSettings>(
    builder.Configuration.GetSection("RessourceplanWorkbook"));

builder.Services.Configure<ProjectWorkspaceSettings>(
    builder.Configuration.GetSection("ProjectWorkspace"));

builder.Services.Configure<CapacityDefaultsSettings>(
    builder.Configuration.GetSection("CapacityDefaults"));

builder.Services.Configure<AbsenceSettings>(
    builder.Configuration.GetSection("Absence"));


builder.Services.AddHttpClient<IMicrosoftGraphUserSourceClient, MicrosoftGraphUserSourceClient>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(60);
});
builder.Services.AddHttpClient<IEntraEmployeeClassificationClient, EntraEmployeeClassificationClient>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(60);
});
builder.Services.AddHttpClient<IEntraUserProfileClient, EntraUserProfileClient>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(60);
});
builder.Services.AddHttpClient<IRessourceplanWorkbookSourceClient, RessourceplanWorkbookSourceClient>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(60);
});
builder.Services.AddScoped<IProjectStatusSyncService, ProjectStatusSyncService>();
builder.Services.AddScoped<IProjectEmployeeGroupSyncService, ProjectEmployeeGroupSyncService>();
builder.Services.AddScoped<IProjectEmployeeSyncService, ProjectEmployeeSyncService>();
builder.Services.AddScoped<IActivitySyncService, ActivitySyncService>();
builder.Services.AddScoped<IProjectActivitySyncService, ProjectActivitySyncService>();
builder.Services.AddScoped<IProjectCustomerSyncService, ProjectCustomerSyncService>();
builder.Services.AddScoped<IProjectGroupSyncService, ProjectGroupSyncService>();
builder.Services.AddScoped<IProjectSyncService, ProjectSyncService>();
builder.Services.AddScoped<IProjectQueryService, ProjectQueryService>();
builder.Services.AddScoped<IProjectManagementService, ProjectManagementService>();
builder.Services.AddScoped<IEconomicProjectNumberAllocator, EconomicProjectNumberAllocator>();
builder.Services.AddScoped<ICustomerPartnerRoleService, CustomerPartnerRoleService>();
builder.Services.AddScoped<IUserSyncService, UserSyncService>();
builder.Services.AddScoped<IEmployeeIdentityService, EmployeeIdentityService>();
builder.Services.AddScoped<ISyncRunService, SyncRunService>();
builder.Services.AddScoped<IInternalPlanningCodeService, InternalPlanningCodeService>();
builder.Services.AddScoped<IOfferService, OfferService>();
builder.Services.AddScoped<ISalesAnalyticsService, SalesAnalyticsService>();
builder.Services.AddScoped<IPlanningTargetService, PlanningTargetService>();
builder.Services.AddScoped<IResourcePlanEntryService, ResourcePlanEntryService>();
builder.Services.AddScoped<IResourcePlanService, ResourcePlanService>();
builder.Services.AddScoped<IResourcePlanScenarioService, ResourcePlanScenarioService>();
builder.Services.AddScoped<IResourcePlanHistoryQueryService, ResourcePlanHistoryQueryService>();
builder.Services.AddScoped<IActivityMatchingService, ActivityMatchingService>();
builder.Services.AddScoped<IProjectMetadataService, ProjectMetadataService>();
builder.Services.AddScoped<IProjectLifecycleLogService, ProjectLifecycleLogService>();
builder.Services.AddScoped<IPublicHolidayCalendarService, PublicHolidayCalendarService>();
builder.Services.AddScoped<IVitaHolidayService, VitaHolidayService>();
builder.Services.AddScoped<IEmployeeCapacityOverrideService, EmployeeCapacityOverrideService>();
builder.Services.AddScoped<IEmployeeCapacityPeriodService, EmployeeCapacityPeriodService>();
builder.Services.AddScoped<IEmployeeCapacityProfileService, EmployeeCapacityProfileService>();
builder.Services.AddScoped<ICapacityScheduleQueryService, CapacityScheduleQueryService>();
builder.Services.AddScoped<ITimeEntryService, TimeEntryService>();
builder.Services.AddScoped<IAbsenceRegistrationService, AbsenceRegistrationService>();
builder.Services.AddScoped<ICustomerService, CustomerService>();
builder.Services.AddScoped<ICompanyContactService, CompanyContactService>();
builder.Services.AddScoped<ILookupService, LookupService>();
builder.Services.AddScoped<IResourcePlanEntryHistoryService, ResourcePlanEntryHistoryService>();
builder.Services.AddScoped<IBusinessEventService, BusinessEventService>();
builder.Services.AddScoped<IEntityChangeLogService, EntityChangeLogService>();
builder.Services.AddScoped<IErrorLogService, ErrorLogService>();
builder.Services.AddScoped<ICorrelationTraceService, CorrelationTraceService>();
builder.Services.AddScoped<IResourcePlanSnapshotService, ResourcePlanSnapshotService>();
if (builder.Configuration.GetValue<bool>("SyncScheduler:Enabled"))
{
    builder.Services.AddHostedService<ScheduledSyncHostedService>();
}

if (builder.Configuration.GetValue<bool>("ProjectWorkspacePoller:Enabled"))
{
    builder.Services.AddHostedService<SharePointWorkspacePollerService>();
}
builder.Services.AddHttpClient<IVirkService, VirkService>(client =>
{
    var virkSettings = builder.Configuration.GetSection("Virk").Get<VirkSettings>();
    client.BaseAddress = new Uri(virkSettings?.BaseUrl ?? "http://distribution.virk.dk");
    client.Timeout = TimeSpan.FromSeconds(15);
});
builder.Services.AddHttpClient<IDawaService, DawaService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(10);
});
builder.Services.AddHttpClient<ISharePointOfferArchiveClient, SharePointOfferArchiveClient>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(60);
});

builder.Services.AddHttpClient<IProjectWorkspaceProvisioningClient, ProjectWorkspaceProvisioningClient>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(90);
});

builder.Services.AddHttpClient<IOutlookCalendarClient, OutlookCalendarClient>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(60);
});
builder.Services.AddScoped<IOutOfOfficeCalendarService, OutOfOfficeCalendarService>();
builder.Services.AddScoped<IOvertimeBalanceQueryService, OvertimeBalanceQueryService>();
builder.Services.AddScoped<IOvertimeAdjustmentService, OvertimeAdjustmentService>();
builder.Services.AddScoped<ITimeEntrySyncService, TimeEntrySyncService>();
builder.Services.AddScoped<IOvertimeBalanceRefreshService, OvertimeBalanceRefreshService>();

builder.Services.AddHttpClient<IPdfCaptureMailClient, PdfCaptureMailClient>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(60);
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("Frontend");

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/ping", () => Results.Ok("pong"))
    .AllowAnonymous();

app.MapHealthChecks("/health")
    .AllowAnonymous();


app.MapControllers();

app.Run();
