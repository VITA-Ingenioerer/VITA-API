using System.ComponentModel.DataAnnotations.Schema;

namespace Vita.Planning.Infrastructure.Data.Entities;

/// <summary>
/// One secondary faglighed for one employee. Local read model only — Microsoft Entra's
/// VITA.SecondaryFagligheder custom security attribute is the authority. Rows here are
/// replaced wholesale whenever classification is read from or written to Entra.
/// </summary>
[Table("user_secondary_fagligheder", Schema = "ext")]
public sealed class UserSecondaryFaglighed
{
    [Column("employee_id")]
    public int EmployeeId { get; set; }

    [Column("faglighed")]
    public string Faglighed { get; set; } = string.Empty;

    public ExtUser? User { get; set; }
}
