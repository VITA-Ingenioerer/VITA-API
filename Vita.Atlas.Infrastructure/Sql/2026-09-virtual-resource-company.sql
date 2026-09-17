-- Gives core.virtual_resources an optional company, so the table covers both kinds of
-- plannable non-employee:
--
--   customer_id IS NULL      -> an unfilled role, e.g. NN-BIM ("we need a BIM person, but not
--                               who yet")
--   customer_id IS NOT NULL  -> a named person at a partner company, e.g. Lars J at PLH
--                               arkitekter
--
-- The kind is derived from this column rather than stored as a flag, so there is no second
-- value that can disagree with it.
--
-- core.company_contacts already models "a named person at a customer", and Lars J belongs
-- there too. It is deliberately not the thing a resource plan points at: core.resource_plans
-- already carries employee_id and virtual_resource_id, and a third nullable FK would mean a
-- third branch in every "employee or virtual" check, in the plan mapper, and in the CHECK
-- constraints. Linking the company carries the information that matters for planning at a
-- fraction of that cost.
--
-- Matches VirtualResourceConfiguration (this repo has no EF migrations project).
-- Safe to run more than once.

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('core.virtual_resources') AND name = 'customer_id'
)
BEGIN
    ALTER TABLE core.virtual_resources ADD customer_id INT NULL;
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys
    WHERE name = 'FK_core_virtual_resources_customer'
      AND parent_object_id = OBJECT_ID('core.virtual_resources')
)
BEGIN
    ALTER TABLE core.virtual_resources
        ADD CONSTRAINT FK_core_virtual_resources_customer
        FOREIGN KEY (customer_id) REFERENCES core.customers (customer_id);
END
GO

-- Filtered: only the externals are indexed, because the NN placeholders all share a NULL and
-- would otherwise make up most of a non-selective index.
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID('core.virtual_resources')
      AND name = 'IX_core_virtual_resources_customer_id'
)
BEGIN
    CREATE INDEX IX_core_virtual_resources_customer_id
        ON core.virtual_resources (customer_id)
        WHERE customer_id IS NOT NULL;
END
GO

-- The code is how the legacy Timer-tabel import matches a row's initials to a virtual
-- resource, so two resources sharing one code would make that match ambiguous.
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID('core.virtual_resources')
      AND name = 'UX_core_virtual_resources_code'
)
   AND NOT EXISTS (
    SELECT code FROM core.virtual_resources GROUP BY code HAVING COUNT(*) > 1
)
BEGIN
    CREATE UNIQUE INDEX UX_core_virtual_resources_code
        ON core.virtual_resources (code);
END
GO
