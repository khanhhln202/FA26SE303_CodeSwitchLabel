-- Contribution tasks do not allocate recording pairs, so they must not consume
-- a wave's pair quota or cause recording task creation to fail.
BEGIN;

DO $$
DECLARE
    definition TEXT;
BEGIN
    SELECT pg_get_functiondef('fn_validate_wave_task_target()'::regprocedure)
      INTO definition;

    definition := replace(
        definition,
        E'BEGIN\n    IF TG_OP',
        E'BEGIN\n    IF NEW.task_type = ''contribution'' THEN\n        RETURN NEW;\n    END IF;\n\n    IF TG_OP');
    definition := replace(
        definition,
        'WHERE t.wave_id = NEW.wave_id',
        'WHERE t.wave_id = NEW.wave_id AND t.task_type <> ''contribution''');
    EXECUTE definition;
END $$;

CREATE OR REPLACE VIEW v_wave_progress AS
SELECT
    c.wave_id,
    c.wave_name,
    c.domain,
    c.target_qty AS wave_target_qty,
    c.start_date,
    c.end_date,
    c.status AS wave_status,
    c.assigned_to,
    COALESCE(SUM(t.target_qty) FILTER (WHERE t.task_type <> 'contribution'), 0) AS allocated_task_qty,
    c.target_qty - COALESCE(SUM(t.target_qty) FILTER (WHERE t.task_type <> 'contribution'), 0) AS remaining_task_qty,
    COUNT(t.task_id) AS task_count,
    COUNT(t.task_id) FILTER (WHERE t.status = 'completed') AS completed_task_count
FROM wave c
LEFT JOIN task t ON t.wave_id = c.wave_id
GROUP BY
    c.wave_id,
    c.wave_name,
    c.domain,
    c.target_qty,
    c.start_date,
    c.end_date,
    c.status,
    c.assigned_to;

COMMIT;
