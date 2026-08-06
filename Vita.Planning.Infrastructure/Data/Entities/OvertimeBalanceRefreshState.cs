using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Vita.Planning.Infrastructure.Data.Entities;

/// <summary>
/// Singleton row (id always 1) tracking the watermark for incremental overtime-balance
/// refresh: employees with ext.time_entries or core.overtime_adjustments changes newer
/// than this are the ones re-materialized on the next refresh.
/// </summary>
[Table("overtime_balance_refresh_state", Schema = "core")]
public sealed class OvertimeBalanceRefreshState
{
    [Key]
    [Column("overtime_balance_refresh_state_id")]
    public int OvertimeBalanceRefreshStateId { get; set; }

    [Column("last_refreshed_at_utc")]
    public DateTime LastRefreshedAtUtc { get; set; }
}
