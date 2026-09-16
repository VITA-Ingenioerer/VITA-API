-- Employee classification (Faglighed / Profession) read model.
--
-- Microsoft Entra custom security attributes are the AUTHORITY for these values:
--     VITA.PrimaryFaglighed      (single value)
--     VITA.SecondaryFagligheder  (multi value)
--     VITA.Profession            (single value)
--
-- Everything below is a local read model/cache so Ressourceplan can filter, group and
-- display without a Graph round-trip per employee. It is refreshed on every successful
-- read/write through EmployeeIdentityService and by the user sync reconciliation.
-- Never treat these columns as the source of truth.
--
-- Idempotent: safe to re-run.

IF COL_LENGTH('ext.users', 'primary_faglighed') IS NULL
BEGIN
    ALTER TABLE ext.users ADD primary_faglighed nvarchar(200) NULL;
END
GO

IF COL_LENGTH('ext.users', 'profession') IS NULL
BEGIN
    ALTER TABLE ext.users ADD profession nvarchar(100) NULL;
END
GO

-- Deliberately a child table rather than a delimited string column: Ressourceplan filters
-- on "does this employee have faglighed X" across the whole roster, which a delimited
-- column can only answer with a LIKE scan. The |a|b| serialization exists ONLY in
-- extensionAttribute2 in Entra (for dynamic membership rules) and is generated on the fly.
IF OBJECT_ID('ext.user_secondary_fagligheder', 'U') IS NULL
BEGIN
    CREATE TABLE ext.user_secondary_fagligheder
    (
        employee_id int            NOT NULL,
        faglighed   nvarchar(200)  NOT NULL,

        CONSTRAINT PK_user_secondary_fagligheder
            PRIMARY KEY (employee_id, faglighed),

        CONSTRAINT FK_user_secondary_fagligheder_users
            FOREIGN KEY (employee_id)
            REFERENCES ext.users (employee_id)
            ON DELETE CASCADE
    );
END
GO

-- The read path is "give me every secondary faglighed for these employees" (employee
-- editor) and "give me every employee with faglighed X" (roster filter). The PK covers
-- the first; this covers the second.
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_user_secondary_fagligheder_faglighed'
      AND object_id = OBJECT_ID('ext.user_secondary_fagligheder'))
BEGIN
    CREATE INDEX IX_user_secondary_fagligheder_faglighed
        ON ext.user_secondary_fagligheder (faglighed)
        INCLUDE (employee_id);
END
GO

-- Same rationale for the primary: "show me everyone whose faglighed is HVAC" reads both
-- this column and the child table above.
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_users_primary_faglighed'
      AND object_id = OBJECT_ID('ext.users'))
BEGIN
    CREATE INDEX IX_users_primary_faglighed
        ON ext.users (primary_faglighed)
        INCLUDE (employee_id);
END
GO
