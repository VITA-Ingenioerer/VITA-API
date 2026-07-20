using System.ComponentModel.DataAnnotations;

namespace Vita.Planning.Application.DTOs;

/// <summary>
/// Only meaningful for a project with no rows in ext.project_activities at all — per e-conomic's
/// own documented default ("if there are no activities linked to a project, employees can
/// register time on any activity"), such a project has no restricted list, so any real activity
/// number is valid for it. This provisions the local row needed to satisfy the FK on
/// resource_plan_entries.ext_project_activity_number, which a project with a real restricted
/// list already has.
/// </summary>
public sealed class EnsureProjectActivityRequest
{
    [Required]
    public int ActivityNumber { get; set; }
}
