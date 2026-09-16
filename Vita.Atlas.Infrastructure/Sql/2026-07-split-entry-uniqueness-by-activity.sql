-- Splits the "one entry per plan/target/day" uniqueness rule into two filtered
-- indexes so an entry can now also be scoped to a project activity
-- (ext_project_activity_number). SQL Server unique indexes disallow duplicate
-- NULLs across otherwise-identical rows for a composite key, so a single index
-- naively extended with ext_project_activity_number would still cap "no
-- activity" entries at one per plan/target/day. Two filtered indexes side-step
-- that: one governs activity-scoped entries, the other governs the classic
-- no-activity entry. Both keep the UX_core_resource_plan_entries_day prefix on
-- purpose — ResourcePlanEntriesController's conflict handling does a substring
-- match on that exact prefix and needs no code change as a result.
--
-- Verify against a dev copy before running in production: confirm two entries
-- with the same (resource_plan_id, planning_target_id, plan_date) and both
-- NULL ext_project_activity_number are rejected, and that two entries with the
-- same triple but different non-null activity ids both succeed.

DROP INDEX UX_core_resource_plan_entries_day ON core.resource_plan_entries;

CREATE UNIQUE INDEX UX_core_resource_plan_entries_day_activity
  ON core.resource_plan_entries (resource_plan_id, planning_target_id, plan_date, ext_project_activity_number)
  WHERE ext_project_activity_number IS NOT NULL;

CREATE UNIQUE INDEX UX_core_resource_plan_entries_day_no_activity
  ON core.resource_plan_entries (resource_plan_id, planning_target_id, plan_date)
  WHERE ext_project_activity_number IS NULL;