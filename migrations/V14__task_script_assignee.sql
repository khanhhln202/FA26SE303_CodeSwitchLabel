-- ============================================================
-- V14 — Assignee per script in recording tasks
--
-- The current baseline schema (docs/codeswitchlabel.sql) has this column,
-- but databases created from earlier versions may not. Task creation now
-- writes it for recording tasks, so bring those databases forward safely.
-- ============================================================

BEGIN;

ALTER TABLE task_script
    ADD COLUMN IF NOT EXISTS assignee_user_id BIGINT
    REFERENCES app_user(user_id);

CREATE INDEX IF NOT EXISTS idx_task_script_assignee
    ON task_script (assignee_user_id);

COMMIT;
