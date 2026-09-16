-- A free-text note per employee, written by the managers in the Medarbejder webpart.
--
-- Lives on ext.users next to primary_faglighed and profession, which are likewise ours to
-- write rather than synced. UserSyncService updates named columns on an existing row, so an
-- ordinary sync leaves the note alone; the one branch that rebuilds a row (when an employee
-- id changes) copies it across explicitly.
--
-- Additive and nullable, so every existing reader — including the deployed time entry app,
-- which reads /users — keeps working untouched.
-- Safe to run more than once.

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('ext.users') AND name = 'note')
BEGIN
    ALTER TABLE ext.users ADD note NVARCHAR(1000) NULL;
END

-- Verification:
-- SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID('ext.users') AND name = 'note';
