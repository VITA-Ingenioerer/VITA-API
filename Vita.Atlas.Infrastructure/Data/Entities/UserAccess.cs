using Vita.Atlas.Domain.Enums;

namespace Vita.Atlas.Infrastructure.Data.Entities;

/// <summary>
/// An explicit access grant for one employee. Rows are the exception, not the rule: with no row
/// an employee's role is derived from the org chart (anyone who is somebody's manager in
/// ext.users is a Manager, everyone else is Standard), and a row overrides that derivation in
/// either direction — it can promote someone who manages nobody, or hold a line manager down to
/// Standard.
///
/// Kept in core rather than as a column on ext.users because ext.users is a sync target: the
/// e-conomic/Graph sync owns those columns and would be free to overwrite them.
/// </summary>
public sealed class UserAccess
{
    public int EmployeeId { get; set; }

    public AccessRole Role { get; set; }

    /// <summary>Why the override exists — shown in the access pane so a grant is not anonymous.</summary>
    public string? Reason { get; set; }

    public string? GrantedBy { get; set; }

    public DateTime GrantedAtUtc { get; set; }

    public string? UpdatedBy { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }

    public ExtUser? Employee { get; set; }
}
