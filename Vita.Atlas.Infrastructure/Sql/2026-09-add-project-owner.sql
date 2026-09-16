-- Adds VITA's own project owner to project metadata.
--
-- Deliberately separate from ext.projects.responsible_employee_number: that one is
-- e-conomic's responsible employee (shown as Projektleder and read-only for us), while this
-- is ours to set and change from the project pane. Holding both means neither overwrites
-- the other on the next e-conomic sync.
--
-- Stored as an e-conomic employee number, matching every other employee reference in the
-- planner, rather than a user id or initials.
-- Safe to run more than once.

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('core.project_metadata')
      AND name = 'project_owner_employee_number')
BEGIN
    ALTER TABLE core.project_metadata
        ADD project_owner_employee_number INT NULL;
END

-- Verification:
-- SELECT COUNT(*) FROM sys.columns
-- WHERE object_id = OBJECT_ID('core.project_metadata') AND name = 'project_owner_employee_number';
