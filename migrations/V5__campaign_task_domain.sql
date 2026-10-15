-- ============================================================
-- V5 — Campaign & Task Domain + Permissions + Delete (TEAM_003)
-- PostgreSQL 13+. Idempotent where possible, single transaction.
-- Run: psql -d codeswitchlabel -f migrations/V5__campaign_task_domain.sql
-- Fresh installs get the same result via docs/codeswitchlabel.sql.
-- ============================================================

BEGIN;

-- ------------------------------------------------------------
-- 1. Add domain column to campaign and task with default 'it_technology'
-- ------------------------------------------------------------
ALTER TABLE campaign ADD COLUMN IF NOT EXISTS domain script_domain NOT NULL DEFAULT 'it_technology';
CREATE INDEX IF NOT EXISTS idx_campaign_domain ON campaign (domain);

ALTER TABLE task ADD COLUMN IF NOT EXISTS domain script_domain NOT NULL DEFAULT 'it_technology';
CREATE INDEX IF NOT EXISTS idx_task_domain ON task (domain);

-- Backfill any existing NULLs (if column existed without NOT NULL)
UPDATE campaign SET domain = 'it_technology' WHERE domain IS NULL;
UPDATE task SET domain = 'it_technology' WHERE domain IS NULL;

-- ------------------------------------------------------------
-- 2. fn_require_task_manager_creator: allow task_manager to create campaign
-- ------------------------------------------------------------
CREATE OR REPLACE FUNCTION fn_require_task_manager_creator() RETURNS trigger AS $$
DECLARE
    v_role_name VARCHAR(32);
BEGIN
    SELECT r.role_name
      INTO v_role_name
      FROM app_user u
      JOIN role r ON r.role_id = u.role_id
     WHERE u.user_id = NEW.created_by;

    IF TG_TABLE_NAME = 'campaign' THEN
        IF v_role_name IS DISTINCT FROM 'admin'
           AND v_role_name IS DISTINCT FROM 'task_manager' THEN
            RAISE EXCEPTION 'campaign creator (user %) must have admin or task_manager role', NEW.created_by;
        END IF;
    ELSIF TG_TABLE_NAME = 'task' THEN
        IF v_role_name IS DISTINCT FROM 'task_manager' THEN
            RAISE EXCEPTION 'task creator (user %) must have task_manager role', NEW.created_by;
        END IF;
    END IF;

    RETURN NEW;
END $$ LANGUAGE plpgsql;

-- ------------------------------------------------------------
-- 3. fn_validate_task_campaign_domain: enforce same domain when attached
-- ------------------------------------------------------------
CREATE OR REPLACE FUNCTION fn_validate_task_campaign_domain() RETURNS trigger AS $$
DECLARE
    v_campaign_domain script_domain;
BEGIN
    -- Detached task: any domain is fine.
    IF NEW.campaign_id IS NULL THEN
        RETURN NEW;
    END IF;

    SELECT c.domain
      INTO v_campaign_domain
      FROM campaign c
     WHERE c.campaign_id = NEW.campaign_id;

    IF NOT FOUND THEN
        RAISE EXCEPTION 'campaign % does not exist', NEW.campaign_id;
    END IF;

    IF NEW.domain IS DISTINCT FROM v_campaign_domain THEN
        RAISE EXCEPTION
            'task % domain % must match campaign % domain %',
            NEW.task_id, NEW.domain, NEW.campaign_id, v_campaign_domain;
    END IF;

    RETURN NEW;
END $$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_task_campaign_domain ON task;
CREATE TRIGGER trg_task_campaign_domain
    BEFORE INSERT OR UPDATE OF campaign_id, domain ON task
    FOR EACH ROW EXECUTE FUNCTION fn_validate_task_campaign_domain();

-- ------------------------------------------------------------
-- 4. fn_validate_campaign_domain_change: prevent orphan tasks
-- ------------------------------------------------------------
CREATE OR REPLACE FUNCTION fn_validate_campaign_domain_change() RETURNS trigger AS $$
DECLARE
    v_mismatched INT;
BEGIN
    IF NEW.domain IS NOT DISTINCT FROM OLD.domain THEN
        RETURN NEW;
    END IF;

    SELECT COUNT(*) INTO v_mismatched
      FROM task t
     WHERE t.campaign_id = NEW.campaign_id
       AND t.domain IS DISTINCT FROM NEW.domain;

    IF v_mismatched > 0 THEN
        RAISE EXCEPTION
            'campaign % still has % task(s) of another domain; move or detach them before changing its domain',
            NEW.campaign_id, v_mismatched;
    END IF;

    RETURN NEW;
END $$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_campaign_domain_change ON campaign;
CREATE TRIGGER trg_campaign_domain_change
    BEFORE UPDATE OF domain ON campaign
    FOR EACH ROW EXECUTE FUNCTION fn_validate_campaign_domain_change();

-- ------------------------------------------------------------
-- 5. Recreate views with domain column
-- ------------------------------------------------------------
DROP VIEW IF EXISTS v_task_progress;
CREATE VIEW v_task_progress AS
SELECT
    t.task_id,
    t.campaign_id,
    c.campaign_name,
    t.task_type,
    t.domain,
    t.status        AS task_status,
    t.target_qty,
    t.deadline,
    (SELECT COUNT(*) FROM task_script ts WHERE ts.task_id = t.task_id) AS scripts_assigned,
    (SELECT COUNT(*) FROM task_script ts
        WHERE ts.task_id = t.task_id AND ts.status = 'completed')      AS scripts_completed,
    (SELECT COUNT(*) FROM review rv WHERE rv.task_id = t.task_id)      AS reviews_done
FROM task t
LEFT JOIN campaign c ON c.campaign_id = t.campaign_id;

DROP VIEW IF EXISTS v_campaign_progress;
CREATE VIEW v_campaign_progress AS
SELECT
    c.campaign_id,
    c.campaign_name,
    c.domain,
    c.target_qty AS campaign_target_qty,
    c.start_date,
    c.end_date,
    c.status AS campaign_status,
    c.assigned_to,
    COALESCE(SUM(t.target_qty), 0) AS allocated_task_qty,
    c.target_qty - COALESCE(SUM(t.target_qty), 0) AS remaining_task_qty,
    COUNT(t.task_id) AS task_count,
    COUNT(t.task_id) FILTER (WHERE t.status = 'completed') AS completed_task_count
FROM campaign c
LEFT JOIN task t ON t.campaign_id = c.campaign_id
GROUP BY
    c.campaign_id,
    c.campaign_name,
    c.domain,
    c.target_qty,
    c.start_date,
    c.end_date,
    c.status,
    c.assigned_to;

COMMIT;
