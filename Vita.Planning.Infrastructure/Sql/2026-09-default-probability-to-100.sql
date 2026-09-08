-- Backfills probability_percent for offers and project metadata that never got a value.
-- A missing percentage means "certain" in the planning model, so NULL is normalised to 100
-- and is_probable_case is set to match (a 100% case is not a probable case).
--
-- Only NULLs are touched: an explicit 0 is a stated probability (a lost/withdrawn case in the
-- source planning sheet) and must survive this script untouched.
-- Safe to run more than once — a second run matches no rows.

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

-- Verification: both should return 0 rows afterwards.
-- SELECT COUNT(*) FROM core.offers            WHERE probability_percent IS NULL;
-- SELECT COUNT(*) FROM core.project_metadata WHERE probability_percent IS NULL;
