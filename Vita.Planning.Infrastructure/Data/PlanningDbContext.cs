using Microsoft.EntityFrameworkCore;
using Vita.Planning.Infrastructure.Data.Entities;

namespace Vita.Planning.Infrastructure.Data;

public sealed class PlanningDbContext : DbContext
{
    public PlanningDbContext(DbContextOptions<PlanningDbContext> options)
        : base(options)
    {
    }

    public DbSet<ExtProject> Projects => Set<ExtProject>();
    public DbSet<ExtUser> Users => Set<ExtUser>();
    public DbSet<OpsSyncRun> SyncRuns => Set<OpsSyncRun>();
    public DbSet<OpsSyncError> SyncErrors => Set<OpsSyncError>();
    public DbSet<OpsError> OpsErrors => Set<OpsError>();
    public DbSet<ExtProjectGroup> ProjectGroups => Set<ExtProjectGroup>();
    public DbSet<ExtProjectCustomer> ProjectCustomers => Set<ExtProjectCustomer>();
    public DbSet<ExtProjectStatus> ProjectStatuses => Set<ExtProjectStatus>();
    public DbSet<ExtActivity> Activities => Set<ExtActivity>();
    public DbSet<ExtProjectActivity> ProjectActivities => Set<ExtProjectActivity>();
    public DbSet<InternalPlanningCode> InternalPlanningCodes => Set<InternalPlanningCode>();
    public DbSet<ExtProjectEmployeeGroup> ProjectEmployeeGroups => Set<ExtProjectEmployeeGroup>();
    public DbSet<ExtProjectEmployee> ProjectEmployees => Set<ExtProjectEmployee>();
    public DbSet<ResourcePlanEntry> ResourcePlanEntries => Set<ResourcePlanEntry>();
    public DbSet<Offer> Offers => Set<Offer>();
    public DbSet<PlanningTarget> PlanningTargets => Set<PlanningTarget>();
    public DbSet<ResourcePlan> ResourcePlans => Set<ResourcePlan>();
    public DbSet<VirtualResource> VirtualResources => Set<VirtualResource>();
    public DbSet<ResourcePlanScenario> ResourcePlanScenarios => Set<ResourcePlanScenario>();
    public DbSet<ProjectMetadata> ProjectMetadata => Set<ProjectMetadata>();
    public DbSet<ProjectLifecycleLog> ProjectLifecycleLogs => Set<ProjectLifecycleLog>();
    public DbSet<PublicHolidayCalendar> PublicHolidayCalendars => Set<PublicHolidayCalendar>();
    public DbSet<EmployeeCapacityOverride> EmployeeCapacityOverrides => Set<EmployeeCapacityOverride>();
    public DbSet<EmployeeCapacityPeriod> EmployeeCapacityPeriods => Set<EmployeeCapacityPeriod>();
    public DbSet<EmployeeCapacityProfile> EmployeeCapacityProfiles => Set<EmployeeCapacityProfile>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<CompanyContact> CompanyContacts => Set<CompanyContact>();
    public DbSet<OfferStatus> OfferStatuses => Set<OfferStatus>();
    public DbSet<CompetitionForm> CompetitionForms => Set<CompetitionForm>();
    public DbSet<EnterpriseForm> EnterpriseForms => Set<EnterpriseForm>();
    public DbSet<ConsultantForm> ConsultantForms => Set<ConsultantForm>();
    public DbSet<ProjectType> ProjectTypes => Set<ProjectType>();
    public DbSet<ProjectRole> ProjectRoles => Set<ProjectRole>();
    public DbSet<ComplexityLevel> ComplexityLevels => Set<ComplexityLevel>();
    public DbSet<EngineeringDiscipline> EngineeringDisciplines => Set<EngineeringDiscipline>();
    public DbSet<Segment> Segments => Set<Segment>();
    public DbSet<OfferSegment> OfferSegments => Set<OfferSegment>();
    public DbSet<ProjectMetadataSegment> ProjectMetadataSegments => Set<ProjectMetadataSegment>();
    public DbSet<CustomerPartnerRole> CustomerPartnerRoles => Set<CustomerPartnerRole>();
    public DbSet<PlanningPartnerRoleType> PlanningPartnerRoleTypes => Set<PlanningPartnerRoleType>();
    public DbSet<ResourcePlanEntryHistory> ResourcePlanEntryHistories => Set<ResourcePlanEntryHistory>();
    public DbSet<BusinessEvent> BusinessEvents => Set<BusinessEvent>();
    public DbSet<ResourcePlanSnapshot> ResourcePlanSnapshots => Set<ResourcePlanSnapshot>();
    public DbSet<ResourcePlanSnapshotEntry> ResourcePlanSnapshotEntries => Set<ResourcePlanSnapshotEntry>();
    public DbSet<ProjectTeamMember> ProjectTeamMembers => Set<ProjectTeamMember>();
    public DbSet<OvertimeAdjustment> OvertimeAdjustments => Set<OvertimeAdjustment>();
    public DbSet<VwOvertimeBalance> OvertimeBalanceDaily => Set<VwOvertimeBalance>();
    public DbSet<ExtTimeEntry> TimeEntries => Set<ExtTimeEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ExtProject>().HasKey(p => p.ProjectNumber);
        modelBuilder.Entity<ExtUser>().HasKey(x => x.EmployeeId);
        modelBuilder.Entity<OpsSyncRun>().HasKey(x => x.SyncRunId);
        modelBuilder.Entity<OpsSyncError>().HasKey(x => x.SyncErrorId);
        modelBuilder.Entity<OpsError>().HasKey(x => x.OpsErrorId);
        modelBuilder.Entity<ExtProjectGroup>().HasKey(x => x.ProjectGroupNumber);
        modelBuilder.Entity<ExtProject>().Property(x => x.Mileage).HasPrecision(18, 2);
        modelBuilder.Entity<ExtProject>().Property(x => x.CostPrice).HasPrecision(18, 2);
        modelBuilder.Entity<ExtProject>().Property(x => x.SalesPrice).HasPrecision(18, 2);
        modelBuilder.Entity<ExtProject>().Property(x => x.FixedPrice).HasPrecision(18, 2);
        modelBuilder.Entity<ExtProject>().Property(x => x.InvoicedTotal).HasPrecision(18, 2);
        modelBuilder.Entity<ExtProjectCustomer>().HasKey(x => x.CustomerNumber);
        modelBuilder.Entity<ExtProjectStatus>().HasKey(x => x.StatusNumber);
        modelBuilder.Entity<ExtProjectEmployeeGroup>().HasKey(x => x.EmployeeGroupNumber);
        modelBuilder.Entity<ExtProjectEmployee>().HasKey(x => x.EmployeeNumber);
        modelBuilder.Entity<ExtActivity>().HasKey(x => x.ActivityNumber);
        modelBuilder.Entity<ExtActivity>().Property(x => x.CostPriceMarkupPercentage).HasPrecision(18, 4);
        modelBuilder.Entity<ExtActivity>().Property(x => x.SalesPriceAfter).HasPrecision(18, 2);
        modelBuilder.Entity<ExtActivity>().Property(x => x.SalesPriceBefore).HasPrecision(18, 2);
        modelBuilder.Entity<ExtProjectActivity>().HasKey(x => x.Number);
        modelBuilder.Entity<ExtProjectActivity>().HasIndex(x => new { x.ProjectNumber, x.ActivityNumber }).IsUnique();
        modelBuilder.Entity<ExtProjectActivity>()
            .HasOne(x => x.Activity)
            .WithMany()
            .HasForeignKey(x => x.ActivityNumber)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PlanningDbContext).Assembly);
        modelBuilder.Entity<ExtUser>()
            .HasIndex(x => x.UserPrincipalName)
            .IsUnique();

        modelBuilder.Entity<OpsSyncError>()
            .HasOne(x => x.SyncRun)
            .WithMany()
            .HasForeignKey(x => x.SyncRunId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<OfferSegment>()
            .HasKey(x => new { x.OfferId, x.SegmentId });

        modelBuilder.Entity<OfferSegment>()
            .HasOne(x => x.Offer)
            .WithMany()
            .HasForeignKey(x => x.OfferId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<OfferSegment>()
            .HasOne(x => x.SegmentEntity)
            .WithMany()
            .HasForeignKey(x => x.SegmentId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<CustomerPartnerRole>()
            .HasOne(x => x.PlanningTarget)
            .WithMany()
            .HasForeignKey(x => x.PlanningTargetId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<CustomerPartnerRole>()
            .HasOne(x => x.Customer)
            .WithMany()
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<CustomerPartnerRole>()
            .HasOne(x => x.PlanningPartnerRoleType)
            .WithMany()
            .HasForeignKey(x => x.PlanningPartnerRoleTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<CustomerPartnerRole>()
            .HasOne(x => x.CompanyContact)
            .WithMany()
            .HasForeignKey(x => x.CompanyContactId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<ResourcePlanEntryHistory>()
            .HasOne(x => x.ResourcePlanEntry)
            .WithMany()
            .HasForeignKey(x => x.ResourcePlanEntryId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<ResourcePlanEntryHistory>()
            .HasOne(x => x.PlanningTarget)
            .WithMany()
            .HasForeignKey(x => x.PlanningTargetId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<BusinessEvent>()
            .HasOne(x => x.PlanningTarget)
            .WithMany()
            .HasForeignKey(x => x.PlanningTargetId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ProjectMetadataSegment>()
            .HasKey(x => new { x.ProjectMetadataId, x.SegmentId });

        modelBuilder.Entity<ProjectMetadataSegment>()
            .HasOne(x => x.ProjectMetadata)
            .WithMany()
            .HasForeignKey(x => x.ProjectMetadataId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ProjectMetadataSegment>()
            .HasOne(x => x.SegmentEntity)
            .WithMany()
            .HasForeignKey(x => x.SegmentId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ProjectTeamMember>()
            .HasOne<ProjectMetadata>()
            .WithMany()
            .HasForeignKey(x => x.ProjectMetadataId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<VwOvertimeBalance>()
            .HasNoKey()
            .ToView("vw_overtime_balance", "core");

        modelBuilder.Entity<ExtTimeEntry>().HasKey(x => x.Number);
        modelBuilder.Entity<ExtTimeEntry>().Property(x => x.NumberOfHours).HasPrecision(9, 2);
        modelBuilder.Entity<ExtTimeEntry>().HasIndex(x => new { x.EmployeeNumber, x.Date });
        modelBuilder.Entity<ExtTimeEntry>().HasIndex(x => x.LastUpdated);
    }
}
