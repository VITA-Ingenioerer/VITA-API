-- Makes core.resource_plan_entries' planning_target_id index covering for the
-- "last resource plan activity" aggregate.
--
-- ProjectQueryService.GetLastResourcePlanActivityAsync and its OfferService twin
-- run MAX(updated_at ?? created_at) grouped per planning target for a page of up
-- to 500 projects/offers. The old non-covering index gave SQL Server the target
-- ids but not the two timestamps, so the aggregate fell back to a key lookup per
-- entry over the whole ~270k-row table — measured at roughly two seconds per page,
-- multiplied by every page the admin/planner web parts fetch on load.
--
-- Pure DDL, no data or behavior change. Matches the IncludeProperties() added to
-- ResourcePlanEntryConfiguration (this repo has no EF migrations project).
--
-- DROP + CREATE rather than a guarded CREATE: the index already exists under the
-- EF-generated name, so it has to be replaced, not skipped. Safe to run more than
-- once; both statements are guarded.

DECLARE @existingIndexId INT = (
    SELECT index_id FROM sys.indexes
    WHERE object_id = OBJECT_ID('core.resource_plan_entries')
      AND name = 'IX_resource_plan_entries_planning_target_id'
);

DECLARE @alreadyCovering BIT = CASE WHEN EXISTS (
    SELECT 1
    FROM sys.index_columns ic
    JOIN sys.columns c
      ON c.object_id = ic.object_id AND c.column_id = ic.column_id
    WHERE ic.object_id = OBJECT_ID('core.resource_plan_entries')
      AND ic.index_id = @existingIndexId
      AND ic.is_included_column = 1
      AND c.name = 'updated_at'
) THEN 1 ELSE 0 END;

IF @existingIndexId IS NOT NULL AND @alreadyCovering = 0
BEGIN
    DROP INDEX IX_resource_plan_entries_planning_target_id
        ON core.resource_plan_entries;
END

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID('core.resource_plan_entries')
      AND name = 'IX_resource_plan_entries_planning_target_id'
)
BEGIN
    CREATE INDEX IX_resource_plan_entries_planning_target_id
        ON core.resource_plan_entries (planning_target_id)
        INCLUDE (updated_at, created_at);
END
