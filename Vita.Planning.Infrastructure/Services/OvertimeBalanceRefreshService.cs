using Microsoft.EntityFrameworkCore;
using Vita.Planning.Application.DTOs;
using Vita.Planning.Application.Interfaces;
using Vita.Planning.Infrastructure.Data;
using Vita.Planning.Infrastructure.Data.Entities;

namespace Vita.Planning.Infrastructure.Services;

public sealed class OvertimeBalanceRefreshService : IOvertimeBalanceRefreshService
{
    private const string SourceSystem = "internal";
    private const string ResourceName = "overtime-balance";
    private const int RefreshStateId = 1;

    private readonly PlanningDbContext _db;
    private readonly ISyncRunService _syncRunService;

    public OvertimeBalanceRefreshService(PlanningDbContext db, ISyncRunService syncRunService)
    {
        _db = db;
        _syncRunService = syncRunService;
    }

    public async Task<OvertimeBalanceRefreshResultDto> RefreshChangedEmployeesAsync(
        string initiatedBy, CancellationToken cancellationToken = default)
    {
        var state = await _db.OvertimeBalanceRefreshStates
            .FirstOrDefaultAsync(x => x.OvertimeBalanceRefreshStateId == RefreshStateId, cancellationToken);

        var watermark = state?.LastRefreshedAtUtc ?? DateTime.MinValue;

        var changedFromTimeEntries = await _db.TimeEntries
            .Where(x => x.SourceLastSyncedAt > watermark)
            .Select(x => x.EmployeeNumber)
            .Distinct()
            .ToListAsync(cancellationToken);

        var changedFromAdjustments = await _db.OvertimeAdjustments
            .Where(x => x.CreatedAtUtc > watermark)
            .Select(x => x.EmployeeId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var employeeIds = changedFromTimeEntries
            .Concat(changedFromAdjustments)
            .Distinct()
            .ToList();

        return await RunRefreshAsync(
            employeeIds, "changed employees since last watermark", initiatedBy, cancellationToken);
    }

    public async Task<OvertimeBalanceRefreshResultDto> RefreshAllAsync(
        string initiatedBy, CancellationToken cancellationToken = default)
    {
        var profiledEmployees = await _db.EmployeeCapacityProfiles
            .Where(x => x.IsActive)
            .Select(x => x.EmployeeId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var timeEntryEmployees = await _db.TimeEntries
            .Select(x => x.EmployeeNumber)
            .Distinct()
            .ToListAsync(cancellationToken);

        var adjustmentEmployees = await _db.OvertimeAdjustments
            .Select(x => x.EmployeeId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var employeeIds = profiledEmployees
            .Concat(timeEntryEmployees)
            .Concat(adjustmentEmployees)
            .Distinct()
            .ToList();

        return await RunRefreshAsync(employeeIds, "all employees", initiatedBy, cancellationToken);
    }

    private async Task<OvertimeBalanceRefreshResultDto> RunRefreshAsync(
        IReadOnlyList<int> employeeIds, string notes, string initiatedBy, CancellationToken cancellationToken)
    {
        var syncRunId = await _syncRunService.StartRunAsync(
            SourceSystem, ResourceName, initiatedBy, notes, cancellationToken);

        var startedAtUtc = DateTime.UtcNow;
        var processed = 0;
        var failed = 0;
        var rowsWritten = 0;

        foreach (var employeeId in employeeIds)
        {
            try
            {
                var rows = await _db.OvertimeBalanceComputedRows
                    .FromSqlInterpolated(BuildComputationQuery(employeeId))
                    .AsNoTracking()
                    .ToListAsync(cancellationToken);

                await _db.OvertimeBalanceDaily
                    .Where(x => x.EmployeeId == employeeId)
                    .ExecuteDeleteAsync(cancellationToken);

                var now = DateTime.UtcNow;
                _db.OvertimeBalanceDaily.AddRange(rows.Select(r => new OvertimeBalanceDaily
                {
                    EmployeeId = r.EmployeeId,
                    WorkDate = r.WorkDate,
                    ActualHours = r.ActualHours,
                    ExpectedHours = r.ExpectedHours,
                    AdjustmentHours = r.AdjustmentHours,
                    DailyDelta = r.DailyDelta,
                    RunningBalance = r.RunningBalance,
                    ComputedAtUtc = now
                }));

                await _db.SaveChangesAsync(cancellationToken);

                rowsWritten += rows.Count;
                processed++;
            }
            catch (Exception ex)
            {
                failed++;
                await _syncRunService.LogErrorAsync(
                    syncRunId, SourceSystem, ResourceName, "refresh-employee",
                    ex.Message, recordKey: employeeId.ToString(), cancellationToken: cancellationToken);
            }
        }

        await UpdateWatermarkAsync(startedAtUtc, cancellationToken);

        var status = failed == 0 ? "succeeded" : processed > 0 ? "partial" : "failed";

        await _syncRunService.CompleteRunAsync(
            syncRunId, status, rowsRead: employeeIds.Count, rowsInserted: rowsWritten,
            errorCount: failed, notes: notes, cancellationToken: cancellationToken);

        return new OvertimeBalanceRefreshResultDto
        {
            SyncRunId = syncRunId,
            EmployeesProcessed = processed,
            EmployeesFailed = failed,
            RowsWritten = rowsWritten,
            Status = status
        };
    }

    private async Task UpdateWatermarkAsync(DateTime refreshStartedAtUtc, CancellationToken cancellationToken)
    {
        var state = await _db.OvertimeBalanceRefreshStates
            .FirstOrDefaultAsync(x => x.OvertimeBalanceRefreshStateId == RefreshStateId, cancellationToken);

        if (state is null)
        {
            state = new OvertimeBalanceRefreshState { OvertimeBalanceRefreshStateId = RefreshStateId };
            _db.OvertimeBalanceRefreshStates.Add(state);
        }

        // Use the time the refresh started, not finished: any change that lands mid-refresh
        // is safer to pick up again next time than to risk missing it.
        state.LastRefreshedAtUtc = refreshStartedAtUtc;

        await _db.SaveChangesAsync(cancellationToken);
    }

    // Same logic as core.vw_overtime_balance, but with the employee filter pushed into
    // employee_range/actual/date_spine before the tally-table cross join, instead of applied
    // as an outer WHERE after the fact — that's what makes this cheap to run per employee
    // instead of recomputing the whole company on every call.
    private static FormattableString BuildComputationQuery(int employeeId) => $"""
        WITH
        n AS (
            SELECT TOP (50000) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) - 1 AS n
            FROM sys.all_objects a CROSS JOIN sys.all_objects b
        ),
        -- Bounded to the LATER of "profile went into effect" and "earliest actual time entry
        -- we have" — never generate a calendar day older than our actual-hours coverage, or
        -- every such day silently contributes a full day of phantom expected-hours debt
        -- (0 actual - full norm) to running_balance forever. This was producing balances in
        -- the tens of thousands of hours for people whose profile predates their time-entry
        -- history by years.
        employee_range AS (
            SELECT
                {employeeId} AS employee_id,
                (SELECT MAX(bound) FROM (VALUES
                    ((SELECT MIN(effective_from) FROM core.employee_capacity_profiles
                      WHERE is_active = 1 AND employee_id = {employeeId})),
                    ((SELECT MIN([date]) FROM ext.time_entries
                      WHERE employee_number = {employeeId} AND is_approved = 1))
                ) AS bounds(bound)) AS start_date
        ),
        calendar AS (
            SELECT er.employee_id,
                   DATEADD(DAY, -n.n, CAST(GETUTCDATE() AS DATE)) AS work_date
            FROM employee_range er
            JOIN n ON DATEADD(DAY, -n.n, CAST(GETUTCDATE() AS DATE)) >= er.start_date
        ),
        date_spine AS (
            SELECT employee_id, work_date FROM calendar
            UNION
            SELECT employee_id, effective_month FROM core.overtime_adjustments WHERE employee_id = {employeeId}
        ),
        actual AS (
            SELECT employee_number AS employee_id,
                   [date]          AS work_date,
                   SUM(number_of_hours) AS actual_hours
            FROM ext.time_entries
            WHERE is_approved = 1 AND employee_number = {employeeId}
            GROUP BY employee_number, [date]
        ),
        base_hours AS (
            SELECT
                ds.employee_id,
                ds.work_date,
                COALESCE(a.actual_hours, 0) AS actual_hours,
                CASE DATEDIFF(DAY, '19000101', ds.work_date) % 7
                    WHEN 0 THEN COALESCE(cp.monday_hours,    cp.default_weekly_hours / 5.0, 0)
                    WHEN 1 THEN COALESCE(cp.tuesday_hours,   cp.default_weekly_hours / 5.0, 0)
                    WHEN 2 THEN COALESCE(cp.wednesday_hours, cp.default_weekly_hours / 5.0, 0)
                    WHEN 3 THEN COALESCE(cp.thursday_hours,  cp.default_weekly_hours / 5.0, 0)
                    WHEN 4 THEN COALESCE(cp.friday_hours,    cp.default_weekly_hours / 5.0, 0)
                    WHEN 5 THEN COALESCE(cp.saturday_hours,  0)
                    WHEN 6 THEN COALESCE(cp.sunday_hours,    0)
                END AS profile_hours,
                CASE
                    WHEN co.employee_capacity_override_id IS NULL THEN NULL
                    WHEN co.monday_hours IS NOT NULL THEN
                        CASE DATEDIFF(DAY, '19000101', ds.work_date) % 7
                            WHEN 0 THEN co.monday_hours
                            WHEN 1 THEN co.tuesday_hours
                            WHEN 2 THEN co.wednesday_hours
                            WHEN 3 THEN co.thursday_hours
                            WHEN 4 THEN co.friday_hours
                            WHEN 5 THEN co.saturday_hours
                            WHEN 6 THEN co.sunday_hours
                        END
                    WHEN co.capacity_factor IS NOT NULL THEN
                        CASE DATEDIFF(DAY, '19000101', ds.work_date) % 7
                            WHEN 0 THEN COALESCE(cp.monday_hours,    cp.default_weekly_hours / 5.0, 0)
                            WHEN 1 THEN COALESCE(cp.tuesday_hours,   cp.default_weekly_hours / 5.0, 0)
                            WHEN 2 THEN COALESCE(cp.wednesday_hours, cp.default_weekly_hours / 5.0, 0)
                            WHEN 3 THEN COALESCE(cp.thursday_hours,  cp.default_weekly_hours / 5.0, 0)
                            WHEN 4 THEN COALESCE(cp.friday_hours,    cp.default_weekly_hours / 5.0, 0)
                            WHEN 5 THEN COALESCE(cp.saturday_hours,  0)
                            WHEN 6 THEN COALESCE(cp.sunday_hours,    0)
                        END * co.capacity_factor
                    WHEN co.weekly_hours IS NOT NULL THEN co.weekly_hours / 5.0
                    ELSE NULL
                END AS override_hours,
                COALESCE(ph.hours_reduction, 0) + COALESCE(vh.hours_reduction, 0) AS holiday_reduction
            FROM date_spine ds
            LEFT JOIN actual a
                ON a.employee_id = ds.employee_id AND a.work_date = ds.work_date
            LEFT JOIN core.employee_capacity_profiles cp
                ON cp.employee_id = ds.employee_id
                AND cp.effective_from <= ds.work_date
                AND (cp.effective_to IS NULL OR cp.effective_to >= ds.work_date)
                AND cp.is_active = 1
            LEFT JOIN core.employee_capacity_overrides co
                ON co.employee_id = ds.employee_id
                AND co.effective_from <= ds.work_date
                AND (co.effective_to IS NULL OR co.effective_to >= ds.work_date)
                AND co.is_active = 1
            LEFT JOIN core.public_holiday_calendar ph
                ON ph.holiday_date = ds.work_date AND ph.country_code = 'DK'
            LEFT JOIN core.vita_holiday_overrides vh
                ON vh.holiday_date = ds.work_date AND vh.is_active = 1
        ),
        daily AS (
            SELECT
                bh.employee_id,
                bh.work_date,
                bh.actual_hours,
                CASE
                    WHEN COALESCE(bh.override_hours, bh.profile_hours, 0) - bh.holiday_reduction < 0 THEN 0
                    ELSE COALESCE(bh.override_hours, bh.profile_hours, 0) - bh.holiday_reduction
                END AS expected_hours,
                -- A regulering is a CHECKPOINT, not a delta: "the flex balance IS this value as
                -- of this date," not "add/subtract this many hours." MAX guards against two
                -- adjustments accidentally sharing the same effective_month multiplying rows.
                (SELECT MAX(oa.hours) FROM core.overtime_adjustments oa
                 WHERE oa.employee_id = bh.employee_id AND oa.effective_month = bh.work_date) AS checkpoint_value
            FROM base_hours bh
        ),
        ordered AS (
            SELECT d.*, ROW_NUMBER() OVER (ORDER BY d.work_date) AS rn
            FROM daily d
        ),
        -- running_balance can't be a flat SUM(...) window anymore: each checkpoint rebases
        -- everything after it, which only a sequential (recursive) walk can express.
        balance (employee_id, work_date, actual_hours, expected_hours, checkpoint_value, rn, running_balance) AS (
            SELECT
                o.employee_id, o.work_date, o.actual_hours, o.expected_hours, o.checkpoint_value, o.rn,
                CAST(COALESCE(o.checkpoint_value, o.actual_hours - o.expected_hours) AS DECIMAL(18, 2))
            FROM ordered o
            WHERE o.rn = 1

            UNION ALL

            SELECT
                o.employee_id, o.work_date, o.actual_hours, o.expected_hours, o.checkpoint_value, o.rn,
                CAST(COALESCE(o.checkpoint_value, b.running_balance + (o.actual_hours - o.expected_hours)) AS DECIMAL(18, 2))
            FROM ordered o
            JOIN balance b ON o.rn = b.rn + 1
        )
        SELECT
            b.employee_id,
            b.work_date,
            b.actual_hours,
            b.expected_hours,
            -- The correction this checkpoint applied, isolated from the normal work-hours
            -- delta — i.e. how far off the naive running total was, not the checkpoint's
            -- absolute value. This is what the chart's "regulering" marker and the weekly
            -- table's "Regulering" column show, and both still mean exactly that.
            CASE
                WHEN b.checkpoint_value IS NULL THEN CAST(0 AS DECIMAL(18, 2))
                ELSE b.running_balance - (b.actual_hours - b.expected_hours)
                     - LAG(b.running_balance, 1, CAST(0 AS DECIMAL(18, 2))) OVER (ORDER BY b.work_date)
            END AS adjustment_hours,
            b.running_balance - LAG(b.running_balance, 1, CAST(0 AS DECIMAL(18, 2))) OVER (ORDER BY b.work_date) AS daily_delta,
            b.running_balance
        FROM balance b
        ORDER BY b.work_date
        OPTION (MAXRECURSION 0);
        """;
}
