-- Adds core.user_access: the explicit half of the Standard/Manager/Admin ladder.
--
-- Most people never get a row here. With no row an employee's role is derived from the org
-- chart — anyone with at least one active direct report in ext.users is a Manager, everyone
-- else is Standard — and a row overrides that derivation in either direction. See
-- UserAccessService for the resolution order (bootstrap allowlist, then this table, then the
-- org chart).
--
-- Kept in core rather than as a column on ext.users because ext.users is a sync target: the
-- e-conomic/Graph sync owns those columns and would be free to overwrite them.
--
-- Matches UserAccessConfiguration (this repo has no EF migrations project).
-- Safe to run more than once.

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('core.user_access'))
BEGIN
    CREATE TABLE core.user_access
    (
        employee_id     INT            NOT NULL,
        -- Matches Vita.Atlas.Domain.Enums.AccessRole: 0 Standard, 1 Manager, 2 Admin.
        -- The ordering is part of the contract — access checks compare with >=.
        role            INT            NOT NULL,
        reason          NVARCHAR(500)  NULL,
        granted_by      NVARCHAR(200)  NULL,
        granted_at_utc  DATETIME2(7)   NOT NULL CONSTRAINT DF_core_user_access_granted_at_utc DEFAULT SYSUTCDATETIME(),
        updated_by      NVARCHAR(200)  NULL,
        updated_at_utc  DATETIME2(7)   NULL,

        -- One grant per person, so a re-grant updates in place and two rows can never
        -- disagree about somebody's role.
        CONSTRAINT PK_core_user_access PRIMARY KEY (employee_id),

        CONSTRAINT FK_core_user_access_ext_users
            FOREIGN KEY (employee_id) REFERENCES ext.users (employee_id)
            ON DELETE CASCADE,

        CONSTRAINT CK_core_user_access_role CHECK (role IN (0, 1, 2))
    );
END

-- Employee 5 is granted Manager explicitly because the derivation would not reach them:
-- the grant is the point, not a mirror of the org chart. Harmless if they also manage
-- somebody — the override and the derivation agree in that case.
IF EXISTS (SELECT 1 FROM ext.users WHERE employee_id = 5)
   AND NOT EXISTS (SELECT 1 FROM core.user_access WHERE employee_id = 5)
BEGIN
    INSERT INTO core.user_access (employee_id, role, reason, granted_by, granted_at_utc)
    VALUES (5, 1, N'Skal kunne redigere medarbejderoplysninger uden at være leder i organisationsdiagrammet.', N'migration', SYSUTCDATETIME());
END
