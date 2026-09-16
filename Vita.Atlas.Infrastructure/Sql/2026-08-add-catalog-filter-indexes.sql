-- Adds indexes on columns already filtered/joined on today with no supporting
-- index — ext.projects.is_closed/is_barred, ext.users.manager_employee_id
-- (UsersController's per-row manager lookup), core.planning_targets.is_active/
-- ext_project_number, and core.employee_capacity_profiles.employee_id (already
-- filtered on every create/update overlap check, not just a future concern).
--
-- Pure DDL, no data or behavior change — matching CREATE INDEX for the
-- HasIndex() calls added to AtlasDbContext.OnModelCreating and to
-- PlanningTargetConfiguration / EmployeeCapacityProfileConfiguration. Does not
-- touch ext.time_entries or anything e-conomic-sourced — economics.dk remains
-- the source of truth; these indexes only speed up reads of the existing local
-- mirror tables.
--
-- Safe to run more than once: each CREATE INDEX is guarded so an
-- already-created index doesn't abort the batch.

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID('ext.projects') AND name = 'IX_ext_projects_is_closed'
)
BEGIN
    CREATE INDEX IX_ext_projects_is_closed ON ext.projects (is_closed);
END

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID('ext.projects') AND name = 'IX_ext_projects_is_barred'
)
BEGIN
    CREATE INDEX IX_ext_projects_is_barred ON ext.projects (is_barred);
END

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID('ext.users') AND name = 'IX_ext_users_manager_employee_id'
)
BEGIN
    CREATE INDEX IX_ext_users_manager_employee_id ON ext.users (manager_employee_id);
END

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID('core.planning_targets') AND name = 'IX_core_planning_targets_is_active'
)
BEGIN
    CREATE INDEX IX_core_planning_targets_is_active ON core.planning_targets (is_active);
END

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID('core.planning_targets') AND name = 'IX_core_planning_targets_ext_project_number'
)
BEGIN
    CREATE INDEX IX_core_planning_targets_ext_project_number ON core.planning_targets (ext_project_number);
END

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID('core.employee_capacity_profiles') AND name = 'IX_core_employee_capacity_profiles_employee_id'
)
BEGIN
    CREATE INDEX IX_core_employee_capacity_profiles_employee_id ON core.employee_capacity_profiles (employee_id);
END
