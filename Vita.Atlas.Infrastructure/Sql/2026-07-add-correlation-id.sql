-- Adds correlation_id to the two audit/history tables that didn't have it yet,
-- so a request's sync-run and project-lifecycle-log entries can be joined into
-- the unified correlation trace alongside business_events and ops.errors.
-- Nullable: existing rows are unaffected and simply have no correlation id.
-- Safe to run more than once: each ALTER is guarded so an already-added column
-- doesn't abort the batch (as happened when this ran against ops.sync_runs).

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('ops.sync_runs') AND name = 'correlation_id'
)
BEGIN
    ALTER TABLE ops.sync_runs ADD correlation_id UNIQUEIDENTIFIER NULL;
END

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('core.project_lifecycle_log') AND name = 'correlation_id'
)
BEGIN
    ALTER TABLE core.project_lifecycle_log ADD correlation_id UNIQUEIDENTIFIER NULL;
END
