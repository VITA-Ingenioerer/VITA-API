-- Projects are plannable/visible by default.
--
-- Background: core.project_metadata.is_visible_in_planner was 0 on 399 of 402 rows, and
-- ProjectQueryService returned false for the ~6300 projects with no metadata row at all,
-- so the admin table showed "Nej" for effectively every project.
--
-- That was not a decision anyone made. It was a default-value bug: the API projected
-- `meta?.IsVisibleInPlanner ?? false`, the admin UI loaded that false into its edit draft,
-- and saving the project wrote the false back into the metadata row. Every save of an
-- unrelated field re-asserted "hidden".
--
-- The code defaults are fixed (ProjectQueryService + the Upsert/Create/Update request
-- DTOs). This aligns the rows that were already written.
--
-- Note: is_visible_in_planner is not currently read by any query — the planner filters on
-- planning_targets.is_plannable and .is_active instead. So this changes no behaviour today;
-- it makes the stored data mean what it says, before anything starts relying on it.
--
-- Idempotent: safe to re-run.

SET NOCOUNT ON;

DECLARE @before int = (SELECT COUNT(*) FROM core.project_metadata WHERE is_visible_in_planner = 0);

UPDATE core.project_metadata
SET is_visible_in_planner = 1
WHERE is_visible_in_planner = 0;

SELECT
    'project_metadata rows set visible' AS metric,
    CAST(@before AS varchar) AS value
UNION ALL
SELECT
    'still hidden (expect 0)',
    CAST((SELECT COUNT(*) FROM core.project_metadata WHERE is_visible_in_planner = 0) AS varchar);
GO

-- Planning targets deliberately set unplannable are left alone. OfferService sets
-- is_plannable = 0 on an offer's target once the offer converts to a project, so that the
-- offer stops competing with the real project in the planner — that is intentional, not
-- the same bug, and must not be reset here.
SELECT
    'planning_targets is_plannable = 0 (left as-is)' AS metric,
    CAST(COUNT(*) AS varchar) AS value
FROM core.planning_targets
WHERE is_plannable = 0;
GO
