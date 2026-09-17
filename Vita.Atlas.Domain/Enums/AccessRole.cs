namespace Vita.Atlas.Domain.Enums;

/// <summary>
/// What a user is allowed to do in Atlas. Deliberately a small ordered ladder rather than a
/// permission matrix: each level is a strict superset of the one below it, so an access check
/// is a single comparison and there is no combination of grants that has to be reasoned about.
/// </summary>
public enum AccessRole
{
    /// <summary>Time entry, resource plan and the project web parts. The default for everyone.</summary>
    Standard = 0,

    /// <summary>Everything Standard has, plus the employee web part and its property writes.</summary>
    Manager = 1,

    /// <summary>Everything, plus granting and revoking access itself.</summary>
    Admin = 2
}
