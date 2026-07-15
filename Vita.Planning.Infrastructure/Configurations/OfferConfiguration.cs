using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vita.Planning.Infrastructure.Data.Entities;

namespace Vita.Planning.Infrastructure.Configurations;

public sealed class OfferConfiguration : IEntityTypeConfiguration<Offer>
{
    public void Configure(EntityTypeBuilder<Offer> builder)
    {
        builder.ToTable("offers", "core");
        builder.HasKey(x => x.OfferId);
        builder.Property(x => x.OfferId).HasColumnName("offer_id");
        builder.Property(x => x.OfferNumber).HasColumnName("offer_number").HasMaxLength(20).IsRequired();
        builder.Property(x => x.Title).HasColumnName("title").HasMaxLength(255).IsRequired();
        builder.Property(x => x.ResponsibleInitials).HasColumnName("responsible_initials").HasMaxLength(20);
        builder.Property(x => x.ResponsibleOfficeCode).HasColumnName("responsible_office_code").HasMaxLength(20);
        builder.Property(x => x.ProjectType).HasColumnName("project_type").HasMaxLength(100);
        builder.Property(x => x.FeeAmount).HasColumnName("fee_amount").HasPrecision(18, 2);
        builder.Property(x => x.ExpectedStartYear).HasColumnName("expected_start_year");
        builder.Property(x => x.ExpectedStartQuarter).HasColumnName("expected_start_quarter");
        builder.Property(x => x.ExpectedEndYear).HasColumnName("expected_end_year");
        builder.Property(x => x.ExpectedEndQuarter).HasColumnName("expected_end_quarter");
        builder.Property(x => x.EstimatedTotalHours).HasColumnName("estimated_total_hours").HasPrecision(18, 2);
        builder.Property(x => x.WeightedHoursOverride).HasColumnName("weighted_hours_override").HasPrecision(18, 2);
        builder.Property(x => x.Notes).HasColumnName("notes").HasMaxLength(1000);
        builder.Property(x => x.SizeDescription).HasColumnName("size_description").HasMaxLength(255);
        builder.Property(x => x.AddToPqCompetition).HasColumnName("add_to_pq_competition");
        builder.Property(x => x.EstimatedCompetitionStartDate).HasColumnName("estimated_competition_start_date");
        builder.Property(x => x.PqSubmissionDate).HasColumnName("pq_submission_date");
        builder.Property(x => x.DeliveredToPq).HasColumnName("delivered_to_pq");
        builder.Property(x => x.HasRelation).HasColumnName("has_relation");
        builder.Property(x => x.ConvertedToProjectNumber).HasColumnName("converted_to_project_number");
        builder.Property(x => x.ConvertedAtUtc).HasColumnName("converted_at_utc");
        builder.Property(x => x.OfferStatusId).HasColumnName("offer_status_id");
        builder.Property(x => x.CustomerId).HasColumnName("customer_id");
        builder.Property(x => x.CustomerContactId).HasColumnName("company_contact_id");
        builder.Property(x => x.IsActive).HasColumnName("is_active");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by").HasMaxLength(100);
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by").HasMaxLength(100);
        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc");

        // Planning metadata
        builder.Property(x => x.PlanningCategory).HasColumnName("planning_category").HasMaxLength(100);
        builder.Property(x => x.PlanningStatus).HasColumnName("planning_status").HasMaxLength(100);
        builder.Property(x => x.DisciplineOwner).HasColumnName("discipline_owner").HasMaxLength(100);
        builder.Property(x => x.DefaultDescription).HasColumnName("default_description").HasColumnType("nvarchar(max)");
        builder.Property(x => x.ColorTag).HasColumnName("color_tag").HasMaxLength(50);
        builder.Property(x => x.PlanningGroup).HasColumnName("planning_group").HasMaxLength(100);
        builder.Property(x => x.Phase).HasColumnName("phase").HasMaxLength(100);
        builder.Property(x => x.ProbabilityPercent).HasColumnName("probability_percent").HasPrecision(5, 2);
        builder.Property(x => x.LastPlanningReviewBy).HasColumnName("last_planning_review_by").HasMaxLength(255);
        builder.Property(x => x.Priority).HasColumnName("priority");
        builder.Property(x => x.IsBillableForPlanning).HasColumnName("is_billable_for_planning");
        builder.Property(x => x.IsAbsence).HasColumnName("is_absence");
        builder.Property(x => x.IsInternal).HasColumnName("is_internal");
        builder.Property(x => x.IsProbableCase).HasColumnName("is_probable_case");
        builder.Property(x => x.IsVisibleInPlanner).HasColumnName("is_visible_in_planner");
        builder.Property(x => x.DailyPlanningEnabled).HasColumnName("daily_planning_enabled");
        builder.Property(x => x.EntrepriseSum).HasColumnName("entreprise_sum").HasPrecision(18, 2);
        builder.Property(x => x.EntrepriseForm).HasColumnName("entreprise_form").HasMaxLength(255);
        builder.Property(x => x.ArealM2).HasColumnName("areal_m2").HasPrecision(10, 2);
        builder.Property(x => x.Raadgivningsform).HasColumnName("raadgivningsform").HasMaxLength(255);
        builder.Property(x => x.Rolle).HasColumnName("rolle").HasMaxLength(255);
        builder.Property(x => x.ByghherreKontaktperson).HasColumnName("bygherre_kontaktperson").HasMaxLength(255);
        builder.Property(x => x.CompetitionFormId).HasColumnName("competition_form_id");
        builder.Property(x => x.EnterpriseFormId).HasColumnName("enterprise_form_id");
        builder.Property(x => x.ConsultantFormId).HasColumnName("consultant_form_id");
        builder.Property(x => x.ProjectTypeId).HasColumnName("project_type_id");
        builder.Property(x => x.ProjectRoleId).HasColumnName("project_role_id");
        builder.Property(x => x.ComplexityLevelId).HasColumnName("complexity_level_id");

        // Offer case SharePoint / Outlook folder links
        builder.Property(x => x.OfferCaseUrl).HasColumnName("offer_case_url").HasColumnType("nvarchar(max)");
        builder.Property(x => x.OfferCasePath).HasColumnName("offer_case_path").HasMaxLength(500);
        builder.Property(x => x.OfferCaseDriveId).HasColumnName("offer_case_drive_id").HasMaxLength(255);
        builder.Property(x => x.OfferCaseFolderItemId).HasColumnName("offer_case_folder_item_id").HasMaxLength(255);
        builder.Property(x => x.OfferCaseOutlookFolderId).HasColumnName("offer_case_outlook_folder_id").HasColumnType("nvarchar(max)");

        // Project archive — populated at conversion time
        builder.Property(x => x.ProjectArchiveUrl).HasColumnName("project_archive_url").HasColumnType("nvarchar(max)");
        builder.Property(x => x.ProjectArchiveSiteId).HasColumnName("project_archive_site_id").HasMaxLength(255);
        builder.Property(x => x.ProjectArchiveDriveId).HasColumnName("project_archive_drive_id").HasMaxLength(255);
        builder.Property(x => x.ProjectArchiveOutlookFolderId).HasColumnName("project_archive_outlook_folder_id").HasColumnType("nvarchar(max)");

        builder.Property(x => x.ProjectDawaId).HasColumnName("project_dawa_id").HasMaxLength(50);
        builder.Property(x => x.ProjectStreetAddress).HasColumnName("project_street_address").HasMaxLength(255);
        builder.Property(x => x.ProjectPostalCode).HasColumnName("project_postal_code").HasMaxLength(10);
        builder.Property(x => x.ProjectCity).HasColumnName("project_city").HasMaxLength(100);
        builder.Property(x => x.ProjectMunicipality).HasColumnName("project_municipality").HasMaxLength(100);
        builder.Property(x => x.ProjectRegion).HasColumnName("project_region").HasMaxLength(100);

        builder.HasIndex(x => x.OfferNumber).IsUnique();
    }
}
