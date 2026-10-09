-- ============================================================
-- V2 — Task independent of Campaign (C1 + C2)
-- PostgreSQL 13+. Idempotent where possible, single transaction.
-- Run: psql -d codeswitchlabel -f migrations/V2__task_independent_of_campaign.sql
-- Fresh installs get the same result via docs/codeswitchlabel.sql.
-- ============================================================

BEGIN;

-- ------------------------------------------------------------
-- 1. task.campaign_id becomes nullable + ON DELETE SET NULL
--    Original DDL used inline REFERENCES, so the constraint name
--    is auto-generated (usually task_campaign_id_fkey). Look it up
--    instead of hardcoding, so the migration re-runs safely.
-- ------------------------------------------------------------
DO $$ DECLARE
    v_conname NAME;
BEGIN
    SELECT c.conname INTO v_conname
      FROM pg_constraint c
      JOIN pg_class t ON t.oid = c.conrelid
      JOIN pg_class f ON f.oid = c.confrelid
     WHERE t.relname = 'task'
       AND f.relname = 'campaign'
       AND c.contype = 'f'
     LIMIT 1;

    IF v_conname IS NOT NULL THEN
        EXECUTE format('ALTER TABLE task DROP CONSTRAINT IF EXISTS %I', v_conname);
    END IF;
END $$;

-- Re-runnable: no-op when already nullable.
ALTER TABLE task ALTER COLUMN campaign_id DROP NOT NULL;

-- Named explicitly going forward; IF NOT EXISTS is not supported
-- for ADD CONSTRAINT, so drop-then-add above keeps it idempotent
-- for the common case. Second run drops the constraint we just
-- created and recreates it identically.
ALTER TABLE task DROP CONSTRAINT IF EXISTS task_campaign_id_fkey;
ALTER TABLE task ADD CONSTRAINT task_campaign_id_fkey
    FOREIGN KEY (campaign_id) REFERENCES campaign(campaign_id) ON DELETE SET NULL;

-- ------------------------------------------------------------
-- 2. fn_validate_task_campaign_window: skip when detached.
-- ------------------------------------------------------------
CREATE OR REPLACE FUNCTION fn_validate_task_campaign_window() RETURNS trigger AS $$
DECLARE
    v_start_date DATE;
    v_end_date   DATE;
BEGIN
    -- Standalone task (NULL campaign_id): no campaign window to check.
    IF NEW.campaign_id IS NULL THEN
        RETURN NEW;
    END IF;

    SELECT start_date, end_date
      INTO v_start_date, v_end_date
      FROM campaign
     WHERE campaign_id = NEW.campaign_id;

    IF NOT FOUND THEN
        RAISE EXCEPTION 'campaign % does not exist', NEW.campaign_id;
    END IF;

    IF NEW.deadline IS NOT NULL
       AND (
           NEW.deadline < v_start_date::timestamptz
           OR NEW.deadline >= (v_end_date + 1)::timestamptz
       ) THEN
        RAISE EXCEPTION
            'task % deadline % must be within campaign % (% to %)',
            NEW.task_id, NEW.deadline, NEW.campaign_id, v_start_date, v_end_date;
    END IF;

    RETURN NEW;
END $$ LANGUAGE plpgsql;

-- Trigger already covers attach/detach: BEFORE INSERT OR UPDATE OF campaign_id, deadline.
-- No trigger DDL change needed (kept for clarity).
DROP TRIGGER IF EXISTS trg_task_campaign_window ON task;
CREATE TRIGGER trg_task_campaign_window
    BEFORE INSERT OR UPDATE OF campaign_id, deadline ON task
    FOR EACH ROW EXECUTE FUNCTION fn_validate_task_campaign_window();

-- ------------------------------------------------------------
-- 3. fn_validate_campaign_task_target: NULL-aware.
--    - Detach (NEW.campaign_id IS NULL) never over-allocates.
--    - Attach (OLD NULL -> NEW not-null) follows the INSERT branch.
--    - Campaign-to-campaign moves keep ascending lock order and
--      never dereference a NULL old campaign.
-- ------------------------------------------------------------
CREATE OR REPLACE FUNCTION fn_validate_campaign_task_target() RETURNS trigger AS $$
DECLARE
    v_new_target       INT;
    v_new_total        BIGINT;
    v_old_target       INT;
    v_first_campaign   BIGINT;
    v_second_campaign  BIGINT;
BEGIN
    IF TG_OP = 'DELETE' THEN
        -- Deleting a task cannot increase allocation.
        RETURN OLD;
    END IF;

    -- Detach: leaving a campaign cannot over-allocate anything.
    IF NEW.campaign_id IS NULL THEN
        RETURN NEW;
    END IF;

    IF TG_OP = 'INSERT' THEN
        SELECT c.target_qty
          INTO v_new_target
          FROM campaign c
         WHERE c.campaign_id = NEW.campaign_id
         FOR UPDATE;

        IF NOT FOUND THEN
            RAISE EXCEPTION 'campaign % does not exist', NEW.campaign_id;
        END IF;

        SELECT COALESCE(SUM(t.target_qty), 0)
          INTO v_new_total
          FROM task t
         WHERE t.campaign_id = NEW.campaign_id;

        IF v_new_total + NEW.target_qty > v_new_target THEN
            RAISE EXCEPTION
                'campaign % target exceeded: allocated task quantity % + task quantity % > campaign target %',
                NEW.campaign_id, v_new_total, NEW.target_qty, v_new_target;
        END IF;

        RETURN NEW;
    END IF;

    -- Attach: OLD.campaign_id IS NULL, NEW.campaign_id IS NOT NULL.
    -- Same checks as INSERT (lock new campaign, count its tasks).
    IF OLD.campaign_id IS NULL THEN
        SELECT c.target_qty
          INTO v_new_target
          FROM campaign c
         WHERE c.campaign_id = NEW.campaign_id
         FOR UPDATE;

        IF NOT FOUND THEN
            RAISE EXCEPTION 'campaign % does not exist', NEW.campaign_id;
        END IF;

        SELECT COALESCE(SUM(t.target_qty), 0)
          INTO v_new_total
          FROM task t
         WHERE t.campaign_id = NEW.campaign_id
           AND t.task_id <> OLD.task_id;

        IF v_new_total + NEW.target_qty > v_new_target THEN
            RAISE EXCEPTION
                'campaign % target exceeded: allocated task quantity % + task quantity % > campaign target %',
                NEW.campaign_id, v_new_total, NEW.target_qty, v_new_target;
        END IF;

        RETURN NEW;
    END IF;

    IF NEW.campaign_id = OLD.campaign_id THEN
        SELECT c.target_qty
          INTO v_new_target
          FROM campaign c
         WHERE c.campaign_id = NEW.campaign_id
         FOR UPDATE;

        IF NOT FOUND THEN
            RAISE EXCEPTION 'campaign % does not exist', NEW.campaign_id;
        END IF;

        SELECT COALESCE(SUM(t.target_qty), 0)
          INTO v_new_total
          FROM task t
         WHERE t.campaign_id = NEW.campaign_id
           AND t.task_id <> OLD.task_id;

        IF v_new_total + NEW.target_qty > v_new_target THEN
            RAISE EXCEPTION
                'campaign % target exceeded: allocated task quantity % + task quantity % > campaign target %',
                NEW.campaign_id, v_new_total, NEW.target_qty, v_new_target;
        END IF;

        RETURN NEW;
    END IF;

    -- UPDATE moving a task from one campaign to another (both non-NULL here).
    -- Lock both campaign rows in ascending campaign_id order.
    v_first_campaign := LEAST(OLD.campaign_id, NEW.campaign_id);
    v_second_campaign := GREATEST(OLD.campaign_id, NEW.campaign_id);

    PERFORM 1
      FROM campaign
     WHERE campaign_id = v_first_campaign
     FOR UPDATE;

    PERFORM 1
      FROM campaign
     WHERE campaign_id = v_second_campaign
     FOR UPDATE;

    SELECT c.target_qty
      INTO v_old_target
      FROM campaign c
     WHERE c.campaign_id = OLD.campaign_id;

    SELECT c.target_qty
      INTO v_new_target
      FROM campaign c
     WHERE c.campaign_id = NEW.campaign_id;

    IF v_old_target IS NULL OR v_new_target IS NULL THEN
        RAISE EXCEPTION 'source or destination campaign does not exist';
    END IF;

    -- The old campaign cannot become over-allocated by moving a task out.
    -- Check only the destination campaign, excluding the row being moved.
    SELECT COALESCE(SUM(t.target_qty), 0)
      INTO v_new_total
      FROM task t
     WHERE t.campaign_id = NEW.campaign_id
       AND t.task_id <> OLD.task_id;

    IF v_new_total + NEW.target_qty > v_new_target THEN
        RAISE EXCEPTION
            'campaign % target exceeded: allocated task quantity % + moved task quantity % > campaign target %',
            NEW.campaign_id, v_new_total, NEW.target_qty, v_new_target;
    END IF;

    RETURN NEW;
END $$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_task_campaign_target ON task;
CREATE TRIGGER trg_task_campaign_target
    BEFORE INSERT OR UPDATE OF campaign_id, target_qty ON task
    FOR EACH ROW EXECUTE FUNCTION fn_validate_campaign_task_target();

-- ------------------------------------------------------------
-- 4. fn_validate_task_creator_assigned: standalone tasks have no
--    campaign to check the assigned manager against, so skip.
--    (Required for C1; without this every NULL insert raises
--    'campaign % does not exist'.)
-- ------------------------------------------------------------
CREATE OR REPLACE FUNCTION fn_validate_task_creator_assigned() RETURNS trigger AS $$
DECLARE
    v_assigned_to BIGINT;
BEGIN
    -- Standalone task: no campaign assignment rule applies.
    IF NEW.campaign_id IS NULL THEN
        RETURN NEW;
    END IF;

    SELECT c.assigned_to
      INTO v_assigned_to
      FROM campaign c
     WHERE c.campaign_id = NEW.campaign_id;

    IF NOT FOUND THEN
        RAISE EXCEPTION 'campaign % does not exist', NEW.campaign_id;
    END IF;

    IF v_assigned_to IS NULL THEN
        RAISE EXCEPTION
            'campaign % has no assigned task manager yet; assign it before creating tasks',
            NEW.campaign_id;
    END IF;

    IF v_assigned_to IS DISTINCT FROM NEW.created_by THEN
        RAISE EXCEPTION
            'task creator (user %) must be the assigned task manager (user %) of campaign %',
            NEW.created_by, v_assigned_to, NEW.campaign_id;
    END IF;

    RETURN NEW;
END $$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_task_creator_assigned ON task;
CREATE TRIGGER trg_task_creator_assigned
    BEFORE INSERT OR UPDATE OF campaign_id, created_by ON task
    FOR EACH ROW EXECUTE FUNCTION fn_validate_task_creator_assigned();

-- ------------------------------------------------------------
-- 5. fn_validate_campaign_target_update: no change needed.
--    It aggregates WHERE campaign_id = NEW.campaign_id, so
--    detached (NULL) tasks are naturally excluded. Verified only.
-- ------------------------------------------------------------

-- ------------------------------------------------------------
-- 6. Views: unattached tasks must still appear in v_task_progress.
--    v_campaign_progress already LEFT JOINs task — verified, no change.
-- ------------------------------------------------------------
CREATE OR REPLACE VIEW v_task_progress AS
SELECT
    t.task_id,
    t.campaign_id,
    c.campaign_name,
    t.task_type,
    t.status        AS task_status,
    t.target_qty,
    t.deadline,
    (SELECT COUNT(*) FROM task_script ts WHERE ts.task_id = t.task_id) AS scripts_assigned,
    (SELECT COUNT(*) FROM task_script ts
        WHERE ts.task_id = t.task_id AND ts.status = 'completed')      AS scripts_completed,
    (SELECT COUNT(*) FROM review rv WHERE rv.task_id = t.task_id)      AS reviews_done
FROM task t
LEFT JOIN campaign c ON c.campaign_id = t.campaign_id;

-- trg_audit_task unchanged: attach/detach is an UPDATE, old/new
-- campaign_id is captured in old_value/new_value automatically.

COMMIT;

-- ============================================================
-- ROLLBACK (as comments only — run manually, in a transaction):
--
-- -- 1. Re-attach standalone tasks first (or cancel them), otherwise the
-- --    NOT NULL restore fails. Tasks are never hard-deleted via the API:
-- --    SELECT task_id FROM task WHERE campaign_id IS NULL;
-- --    UPDATE task SET campaign_id = <campaign_id> WHERE campaign_id IS NULL;
-- -- 2. ALTER TABLE task ALTER COLUMN campaign_id SET NOT NULL;
-- -- 3. ALTER TABLE task DROP CONSTRAINT IF EXISTS task_campaign_id_fkey;
-- --    ALTER TABLE task ADD CONSTRAINT task_campaign_id_fkey
-- --      FOREIGN KEY (campaign_id) REFERENCES campaign(campaign_id);
-- --      -- (original: no ON DELETE action)
-- -- 4. Restore the three functions and v_task_progress (INNER JOIN)
-- --    from git history of docs/codeswitchlabel.sql (pre-V2).
-- ============================================================
