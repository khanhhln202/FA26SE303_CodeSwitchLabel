-- ============================================================
-- V2 verification (psql): 9 checks for task-independent-of-campaign.
-- Runs in a transaction that ROLLS BACK, so it never pollutes data.
-- Run: psql -d codeswitchlabel -v ON_ERROR_STOP=1 -f migrations/V2__verify_task_independent.sql
-- Requires: an admin user and a task_manager user to exist.
-- ============================================================
BEGIN;

DO $$
DECLARE
    v_admin BIGINT;
    v_manager BIGINT;
    v_c1 BIGINT;
    v_c2 BIGINT;
    v_t_standalone BIGINT;
    v_t_full BIGINT;
    v_t_move BIGINT;
    v_script VARCHAR(11);
    v_n INT;
BEGIN
    SELECT user_id INTO v_admin FROM app_user
     WHERE role_id = (SELECT role_id FROM role WHERE role_name = 'admin') LIMIT 1;
    SELECT user_id INTO v_manager FROM app_user
     WHERE role_id = (SELECT role_id FROM role WHERE role_name = 'task_manager') LIMIT 1;
    IF v_admin IS NULL OR v_manager IS NULL THEN
        RAISE EXCEPTION 'need one admin and one task_manager user for verification';
    END IF;

    -- Setup: two campaigns (target 2000 = schema minimum), one validated script.
    INSERT INTO campaign (campaign_name, target_qty, start_date, end_date, status, created_by, assigned_to)
    VALUES ('V2 verify A', 2000, CURRENT_DATE - 1, CURRENT_DATE + 30, 'open', v_admin, v_manager)
    RETURNING campaign_id INTO v_c1;
    INSERT INTO campaign (campaign_name, target_qty, start_date, end_date, status, created_by, assigned_to)
    VALUES ('V2 verify B', 2000, CURRENT_DATE - 1, CURRENT_DATE + 30, 'open', v_admin, v_manager)
    RETURNING campaign_id INTO v_c2;

    -- 1. Insert task with NULL campaign_id succeeds.
    INSERT INTO task (campaign_id, created_by, task_type, target_qty, deadline)
    VALUES (NULL, v_manager, 'recording', 1, now() + INTERVAL '7 days')
    RETURNING task_id INTO v_t_standalone;
    RAISE NOTICE '1. standalone insert OK (task %)', v_t_standalone;

    -- 2. Attach within quota + window succeeds.
    UPDATE task SET campaign_id = v_c1 WHERE task_id = v_t_standalone;
    RAISE NOTICE '2. attach OK';

    -- 3. Attach fails when exceeding campaign.target_qty.
    INSERT INTO task (campaign_id, created_by, task_type, target_qty, deadline)
    VALUES (v_c1, v_manager, 'recording', 1999, now() + INTERVAL '7 days')
    RETURNING task_id INTO v_t_full;
    BEGIN
        UPDATE task SET campaign_id = v_c1, target_qty = 2 WHERE task_id = v_t_standalone;
        RAISE EXCEPTION '3. FAILED: over-quota attach did not raise';
    EXCEPTION WHEN raise_exception THEN
        IF SQLERRM NOT LIKE '%target exceeded%' THEN RAISE; END IF;
        RAISE NOTICE '3. over-quota blocked OK';
    END;

    -- 4. Attach fails when deadline outside campaign window.
    -- Detach first (window check skipped for NULL), set a far deadline,
    -- then attach to B (ends +30d) with deadline +60d -> must raise.
    UPDATE task SET campaign_id = NULL WHERE task_id = v_t_standalone;
    UPDATE task SET deadline = now() + INTERVAL '60 days' WHERE task_id = v_t_standalone;
    BEGIN
        UPDATE task SET campaign_id = v_c2 WHERE task_id = v_t_standalone;
        RAISE EXCEPTION '4. FAILED: out-of-window attach did not raise';
    EXCEPTION WHEN raise_exception THEN
        IF SQLERRM LIKE '%4. FAILED%' THEN RAISE; END IF;
        IF SQLERRM NOT LIKE '%must be within campaign%' THEN RAISE; END IF;
        RAISE NOTICE '4. out-of-window blocked OK';
    END;
    UPDATE task SET deadline = now() + INTERVAL '7 days' WHERE task_id = v_t_standalone;
    UPDATE task SET campaign_id = v_c1 WHERE task_id = v_t_standalone;

    -- 5. Detach keeps task + child rows.
    SELECT script_id INTO v_script FROM script WHERE status = 'validated' LIMIT 1;
    IF v_script IS NOT NULL THEN
        INSERT INTO task_script (task_id, script_id) VALUES (v_t_standalone, v_script)
        ON CONFLICT DO NOTHING;
    END IF;
    INSERT INTO task_assignment (task_id, user_id) VALUES (v_t_standalone, v_manager)
    ON CONFLICT DO NOTHING;
    UPDATE task SET campaign_id = NULL WHERE task_id = v_t_standalone;
    SELECT COUNT(*) INTO v_n FROM task WHERE task_id = v_t_standalone;
    IF v_n <> 1 THEN RAISE EXCEPTION '5. FAILED: task row lost on detach'; END IF;
    SELECT COUNT(*) INTO v_n FROM task_assignment WHERE task_id = v_t_standalone;
    IF v_n < 1 THEN RAISE EXCEPTION '5. FAILED: assignments lost on detach'; END IF;
    RAISE NOTICE '5. detach preserves rows OK';

    -- 8. v_task_progress lists unattached tasks (LEFT JOIN).
    SELECT COUNT(*) INTO v_n FROM v_task_progress WHERE task_id = v_t_standalone;
    IF v_n <> 1 THEN RAISE EXCEPTION '8. FAILED: unattached task missing from v_task_progress'; END IF;
    RAISE NOTICE '8. v_task_progress lists unattached OK';

    -- 9. Move between campaigns works.
    INSERT INTO task (campaign_id, created_by, task_type, target_qty, deadline)
    VALUES (v_c1, v_manager, 'recording', 1, now() + INTERVAL '7 days')
    RETURNING task_id INTO v_t_move;
    UPDATE task SET campaign_id = v_c2 WHERE task_id = v_t_move;
    RAISE NOTICE '9. move OK';

    -- 6. Deleting a campaign SETs NULL instead of deleting tasks.
    DELETE FROM campaign WHERE campaign_id = v_c2;
    SELECT COUNT(*) INTO v_n FROM task WHERE task_id = v_t_move AND campaign_id IS NULL;
    IF v_n <> 1 THEN RAISE EXCEPTION '6. FAILED: campaign delete did not SET NULL'; END IF;
    RAISE NOTICE '6. campaign delete SET NULL OK';

    -- 7. Cancel keeps the row (no hard delete in project): task stays
    --    Cancelled with its child rows instead of being removed.
    UPDATE task SET status = 'cancelled' WHERE task_id = v_t_move;
    SELECT COUNT(*) INTO v_n FROM task WHERE task_id = v_t_move AND status = 'cancelled';
    IF v_n <> 1 THEN RAISE EXCEPTION '7. FAILED: cancelled task row lost'; END IF;
    RAISE NOTICE '7. cancel preserves row OK';

    RAISE NOTICE 'ALL V2 CHECKS PASSED';
END $$;

ROLLBACK;
