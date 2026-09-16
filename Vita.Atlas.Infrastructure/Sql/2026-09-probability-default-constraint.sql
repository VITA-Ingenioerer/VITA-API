-- Makes "no probability stated" impossible to store, rather than only cleaning it up after
-- the fact. 2026-09-default-probability-to-100.sql backfilled the rows that already had
-- NULL; this adds a DEFAULT so a row inserted without the column still lands on 100, and
-- re-runs the backfill so it does not matter which of the two scripts ran first (or whether
-- the earlier one ran at all).
--
-- Only NULLs are touched: an explicit 0 is a stated probability (a lost/withdrawn case in
-- the source planning sheet) and must survive untouched.
--
-- The columns stay nullable on purpose. NOT NULL would need every insert path audited first,
-- and the API already coalesces on write (PlanningDefaults.ProbabilityPercent) and on read.
-- Safe to run more than once — each step checks for its own prior effect.

IF NOT EXISTS (
    SELECT 1 FROM sys.default_constraints
    WHERE parent_object_id = OBJECT_ID('core.offers')
      AND name = 'DF_core_offers_probability_percent')
BEGIN
    ALTER TABLE core.offers
        ADD CONSTRAINT DF_core_offers_probability_percent
        DEFAULT (100) FOR probability_percent;
END

IF NOT EXISTS (
    SELECT 1 FROM sys.default_constraints
    WHERE parent_object_id = OBJECT_ID('core.project_metadata')
      AND name = 'DF_core_project_metadata_probability_percent')
BEGIN
    ALTER TABLE core.project_metadata
        ADD CONSTRAINT DF_core_project_metadata_probability_percent
        DEFAULT (100) FOR probability_percent;
END

-- is_probable_case is set alongside for the same reason the earlier script did it: a 100%
-- case is not a probable case. Keeping both scripts identical on that point means it makes
-- no difference which one cleaned a given row.
UPDATE core.offers
SET probability_percent = 100,
    is_probable_case = 0,
    updated_by = 'probability-default-backfill',
    updated_at_utc = SYSUTCDATETIME()
WHERE probability_percent IS NULL;

UPDATE core.project_metadata
SET probability_percent = 100,
    is_probable_case = 0,
    updated_by = 'probability-default-backfill',
    updated_at_utc = SYSUTCDATETIME()
WHERE probability_percent IS NULL;

-- Verification: both should return 0.
-- SELECT COUNT(*) FROM core.offers           WHERE probability_percent IS NULL;
-- SELECT COUNT(*) FROM core.project_metadata WHERE probability_percent IS NULL;
