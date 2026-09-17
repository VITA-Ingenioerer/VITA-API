using Microsoft.EntityFrameworkCore;
using Vita.Atlas.Infrastructure.Data.Entities;

namespace Vita.Atlas.Infrastructure.Data;

public sealed class AtlasDbContext : DbContext
{
    public AtlasDbContext(DbContextOptions<AtlasDbContext> options)
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
    public DbSet<OfferDiscipline> OfferDisciplines => Set<OfferDiscipline>();
    public DbSet<ProjectMetadataDiscipline> ProjectMetadataDisciplines => Set<ProjectMetadataDiscipline>();
    public DbSet<CustomerPartnerRole> CustomerPartnerRoles => Set<CustomerPartnerRole>();
    public DbSet<PlanningPartnerRoleType> PlanningPartnerRoleTypes => Set<PlanningPartnerRoleType>();
    public DbSet<ResourcePlanEntryHistory> ResourcePlanEntryHistories => Set<ResourcePlanEntryHistory>();
    public DbSet<BusinessEvent> BusinessEvents => Set<BusinessEvent>();
    public DbSet<ResourcePlanSnapshot> ResourcePlanSnapshots => Set<ResourcePlanSnapshot>();
    public DbSet<ResourcePlanSnapshotEntry> ResourcePlanSnapshotEntries => Set<ResourcePlanSnapshotEntry>();
    public DbSet<ProjectTeamMember> ProjectTeamMembers => Set<ProjectTeamMember>();
    public DbSet<OvertimeAdjustment> OvertimeAdjustments => Set<OvertimeAdjustment>();
    public DbSet<OvertimeBalanceDaily> OvertimeBalanceDaily => Set<OvertimeBalanceDaily>();
    public DbSet<OvertimeBalanceRefreshState> OvertimeBalanceRefreshStates => Set<OvertimeBalanceRefreshState>();
    public DbSet<OvertimeBalanceComputedRow> OvertimeBalanceComputedRows => Set<OvertimeBalanceComputedRow>();
    public DbSet<ExtTimeEntry> TimeEntries => Set<ExtTimeEntry>();
    public DbSet<UserSecondaryFaglighed> UserSecondaryFagligheder => Set<UserSecondaryFaglighed>();
    public DbSet<UserAccess> UserAccess => Set<UserAccess>();

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
        // Frontends filter the project catalog by these flags (e.g. hide closed/barred
        // projects from pickers); without an index, that's a full table scan as the
        // catalog grows. Requires the matching CREATE INDEX in the hand-run SQL script —
        // see Sql/2026-08-add-catalog-filter-indexes.sql.
        modelBuilder.Entity<ExtProject>().HasIndex(x => x.IsClosed);
        modelBuilder.Entity<ExtProject>().HasIndex(x => x.IsBarred);
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
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AtlasDbContext).Assembly);
        modelBuilder.Entity<ExtUser>()
            .HasIndex(x => x.UserPrincipalName)
            .IsUnique();
        // UsersController resolves each user's manager via a correlated subquery on this
        // column for every row — unindexed, that's a per-row scan as the roster grows.
        // Requires the matching CREATE INDEX in the hand-run SQL script — see
        // Sql/2026-08-add-catalog-filter-indexes.sql.
        modelBuilder.Entity<ExtUser>().HasIndex(x => x.ManagerEmployeeId);

        modelBuilder.Entity<UserSecondaryFaglighed>(entity =>
        {
            entity.HasKey(x => new { x.EmployeeId, x.Faglighed });

            entity.Property(x => x.Faglighed).HasMaxLength(200).IsRequired();

            entity.HasOne(x => x.User)
                .WithMany(x => x.SecondaryFagligheder)
                .HasForeignKey(x => x.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade);

            // "Which employees have faglighed X" is a roster-wide filter in the planner,
            // and the composite PK above is no help for it (wrong leading column).
            // Requires the matching CREATE INDEX in the hand-run SQL script — see
            // Sql/2026-09-employee-classification.sql.
            entity.HasIndex(x => x.Faglighed);
        });

        modelBuilder.Entity<ExtUser>().Property(x => x.PrimaryFaglighed).HasMaxLength(200);
        modelBuilder.Entity<ExtUser>().Property(x => x.Profession).HasMaxLength(100);
        modelBuilder.Entity<ExtUser>().HasIndex(x => x.PrimaryFaglighed);

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

        modelBuilder.Entity<OfferDiscipline>()
            .HasIndex(x => new { x.OfferId, x.EngineeringDisciplineId })
            .IsUnique();

        modelBuilder.Entity<OfferDiscipline>()
            .HasOne(x => x.Offer)
            .WithMany()
            .HasForeignKey(x => x.OfferId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<OfferDiscipline>()
            .HasOne(x => x.Discipline)
            .WithMany()
            .HasForeignKey(x => x.EngineeringDisciplineId)
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

        modelBuilder.Entity<ProjectMetadataDiscipline>()
            .HasIndex(x => new { x.ProjectMetadataId, x.EngineeringDisciplineId })
            .IsUnique();

        modelBuilder.Entity<ProjectMetadataDiscipline>()
            .HasOne(x => x.ProjectMetadata)
            .WithMany()
            .HasForeignKey(x => x.ProjectMetadataId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ProjectMetadataDiscipline>()
            .HasOne(x => x.Discipline)
            .WithMany()
            .HasForeignKey(x => x.EngineeringDisciplineId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ProjectTeamMember>()
            .HasOne<ProjectMetadata>()
            .WithMany()
            .HasForeignKey(x => x.ProjectMetadataId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<OvertimeBalanceDaily>().HasKey(x => new { x.EmployeeId, x.WorkDate });
        modelBuilder.Entity<OvertimeBalanceComputedRow>().HasNoKey();

        modelBuilder.Entity<ExtTimeEntry>().HasKey(x => x.Number);
        modelBuilder.Entity<ExtTimeEntry>().Property(x => x.NumberOfHours).HasPrecision(9, 2);
        modelBuilder.Entity<ExtTimeEntry>().HasIndex(x => new { x.EmployeeNumber, x.Date });
        modelBuilder.Entity<ExtTimeEntry>().HasIndex(x => x.LastUpdated);
    }
}
