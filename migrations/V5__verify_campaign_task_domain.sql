-- ============================================================
-- V5 verification (psql): checks for campaign & task domain + permissions.
-- Runs in a transaction that ROLLS BACK, so it never pollutes data.
-- Run: psql -d codeswitchlabel -v ON_ERROR_STOP=1 -f migrations/V5__verify_campaign_task_domain.sql
-- ============================================================
BEGIN;

DO $$
DECLARE
    v_admin BIGINT;
    v_manager BIGINT;
    v_speaker BIGINT;
    v_c_it BIGINT;
    v_c_edu BIGINT;
    v_t_it BIGINT;
    v_t_edu BIGINT;
BEGIN
    SELECT user_id INTO v_admin FROM app_user
     WHERE role_id = (SELECT role_id FROM role WHERE role_name = 'admin') LIMIT 1;
    SELECT user_id INTO v_manager FROM app_user
     WHERE role_id = (SELECT role_id FROM role WHERE role_name = 'task_manager') LIMIT 1;
    SELECT user_id INTO v_speaker FROM app_user
     WHERE role_id = (SELECT role_id FROM role WHERE role_name = 'speaker') LIMIT 1;

    IF v_admin IS NULL OR v_manager IS NULL OR v_speaker IS NULL THEN
        RAISE EXCEPTION 'need admin, task_manager and speaker users for verification';
    END IF;

    -- 1. Task Manager CAN create campaign (trg_campaign_creator_role allows task_manager)
    INSERT INTO campaign (campaign_name, domain, target_qty, start_date, end_date, status, created_by, assigned_to)
    VALUES ('V5 test IT', 'it_technology', 2000, CURRENT_DATE - 1, CURRENT_DATE + 30, 'open', v_manager, v_manager)
    RETURNING campaign_id INTO v_c_it;
    RAISE NOTICE '1. TaskManager created campaign OK (id %)', v_c_it;

    -- 2. Speaker CANNOT create campaign
    BEGIN
        INSERT INTO campaign (campaign_name, domain, target_qty, start_date, end_date, status, created_by, assigned_to)
        VALUES ('V5 test Speaker', 'it_technology', 2000, CURRENT_DATE - 1, CURRENT_DATE + 30, 'open', v_speaker, v_manager);
        RAISE EXCEPTION '2. FAILED: speaker was able to create campaign';
    EXCEPTION WHEN raise_exception THEN
        IF SQLERRM NOT LIKE '%admin or task_manager role%' THEN RAISE; END IF;
        RAISE NOTICE '2. Speaker blocked from creating campaign OK';
    END;

    -- Create another campaign with domain 'education'
    INSERT INTO campaign (campaign_name, domain, target_qty, start_date, end_date, status, created_by, assigned_to)
    VALUES ('V5 test EDU', 'education', 2000, CURRENT_DATE - 1, CURRENT_DATE + 30, 'open', v_manager, v_manager)
    RETURNING campaign_id INTO v_c_edu;

    -- 3. Task with domain 'it_technology'
    INSERT INTO task (campaign_id, created_by, task_type, domain, target_qty, deadline)
    VALUES (NULL, v_manager, 'recording', 'it_technology', 10, now() + INTERVAL '7 days')
    RETURNING task_id INTO v_t_it;
    RAISE NOTICE '3. Created standalone IT task (id %)', v_t_it;

    -- 4. IT task attached to IT campaign -> SUCCEEDS
    UPDATE task SET campaign_id = v_c_it WHERE task_id = v_t_it;
    RAISE NOTICE '4. IT task attached to IT campaign OK';

    -- 5. IT task attached to Education campaign -> BLOCKED by trigger
    BEGIN
        UPDATE task SET campaign_id = v_c_edu WHERE task_id = v_t_it;
        RAISE EXCEPTION '5. FAILED: IT task attached to EDU campaign did not raise';
    EXCEPTION WHEN raise_exception THEN
        IF SQLERRM NOT LIKE '%must match campaign%' THEN RAISE; END IF;
        RAISE NOTICE '5. Domain mismatch attach blocked OK';
    END;

    -- 6. Creating task directly in mismatched campaign -> BLOCKED
    BEGIN
        INSERT INTO task (campaign_id, created_by, task_type, domain, target_qty, deadline)
        VALUES (v_c_edu, v_manager, 'recording', 'it_technology', 10, now() + INTERVAL '7 days');
        RAISE EXCEPTION '6. FAILED: created IT task in EDU campaign';
    EXCEPTION WHEN raise_exception THEN
        IF SQLERRM NOT LIKE '%must match campaign%' THEN RAISE; END IF;
        RAISE NOTICE '6. Domain mismatch create blocked OK';
    END;

    -- 7. Changing campaign domain while tasks attached -> BLOCKED
    BEGIN
        UPDATE campaign SET domain = 'daily_life' WHERE campaign_id = v_c_it;
        RAISE EXCEPTION '7. FAILED: changed campaign domain with attached tasks';
    EXCEPTION WHEN raise_exception THEN
        IF SQLERRM NOT LIKE '%still has%task(s) of another domain%' THEN RAISE; END IF;
        RAISE NOTICE '7. Campaign domain change with attached tasks blocked OK';
    END;

    RAISE NOTICE 'ALL V5 CHECKS PASSED';
END $$;

ROLLBACK;
