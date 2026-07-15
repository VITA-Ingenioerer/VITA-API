-- Adds correlation_id to the two audit/history tables that didn't have it yet,
-- so a request's sync-run and project-lifecycle-log entries can be joined into
-- the unified correlation trace alongside business_events and ops.errors.
-- Nullable: existing rows are unaffected and simply have no correlation id.

ALTER TABLE ops.sync_runs
  ADD correlation_id UNIQUEIDENTIFIER NULL;

ALTER TABLE core.project_lifecycle_log
  ADD correlation_id UNIQUEIDENTIFIER NULL;
