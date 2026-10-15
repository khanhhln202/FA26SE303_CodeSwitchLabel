-- ============================================================
-- V6 verification (psql): checks for SQL bugfixes and constraints.
-- Runs in a transaction that ROLLS BACK, so it never pollutes data.
-- Run: psql -d codeswitchlabel -v ON_ERROR_STOP=1 -f migrations/V6__verify_sql_bugfixes.sql
-- ============================================================
BEGIN;

DO $$
DECLARE
    v_admin      BIGINT;
    v_manager    BIGINT;
    v_speaker    BIGINT;
    v_reviewer   BIGINT;
    v_script_it  VARCHAR(11);
    v_script_edu VARCHAR(11);
    v_task_it    BIGINT;
    v_rec_id     VARCHAR(20);
    v_dataset_id BIGINT;
    v_reason_id  SMALLINT;
BEGIN
    SELECT user_id INTO v_admin FROM app_user
     WHERE role_id = (SELECT role_id FROM role WHERE role_name = 'admin') LIMIT 1;
    SELECT user_id INTO v_manager FROM app_user
     WHERE role_id = (SELECT role_id FROM role WHERE role_name = 'task_manager') LIMIT 1;
    SELECT user_id INTO v_speaker FROM app_user
     WHERE role_id = (SELECT role_id FROM role WHERE role_name = 'speaker') LIMIT 1;
    SELECT user_id INTO v_reviewer FROM app_user
     WHERE role_id = (SELECT role_id FROM role WHERE role_name = 'reviewer') LIMIT 1;
    SELECT reason_id INTO v_reason_id FROM script_error_reason LIMIT 1;

    IF v_admin IS NULL OR v_manager IS NULL OR v_speaker IS NULL OR v_reviewer IS NULL THEN
        RAISE EXCEPTION 'need admin, manager, speaker, reviewer users for verification';
    END IF;

    -- 1. Test script creation & duplicate prevention
    INSERT INTO script (script_id, cs_content, vi_content, domain, status, created_by)
    VALUES ('s_111000001', 'Test [EN] server [VI] chu', 'May chu', 'it_technology', 'validated', v_admin)
    ON CONFLICT (script_id) DO NOTHING;

    BEGIN
        INSERT INTO script (script_id, cs_content, vi_content, domain, status, created_by)
        VALUES ('s_111000099', 'Test [EN] server [VI] chu', 'May chu 2', 'it_technology', 'validated', v_admin);
        RAISE EXCEPTION '1. FAILED: duplicate cs_content was accepted';
    EXCEPTION WHEN unique_violation THEN
        RAISE NOTICE '1. Duplicate cs_content blocked OK';
    END;

    -- 2. Test takes 10-19 allowed, take 0/1 disallowed
    BEGIN
        INSERT INTO recording (recording_id, sentence_variant, script_id, speaker_id, cloud_link, duration_sec, status)
        VALUES ('r_cs_111000001_t10', 'code_switching', 's_111000001', v_speaker, 'http://test/10', 5.0, 'pending_review');
        RAISE NOTICE '2a. Take 10 allowed OK';
    END;

    BEGIN
        INSERT INTO recording (recording_id, sentence_variant, script_id, speaker_id, cloud_link, duration_sec, status)
        VALUES ('r_cs_111000001_t1', 'code_switching', 's_111000001', v_speaker, 'http://test/1', 5.0, 'pending_review');
        RAISE EXCEPTION '2b. FAILED: take 1 (_t1) was accepted';
    EXCEPTION WHEN check_violation THEN
        RAISE NOTICE '2b. Take 1 (_t1) blocked by check constraint OK';
    END;

    -- 3. Test script_review rejection reason check
    BEGIN
        INSERT INTO script_review (script_id, user_id, action, error_reason_id)
        VALUES ('s_111000001', v_reviewer, 'rejected', NULL);
        RAISE EXCEPTION '3. FAILED: script_review rejected without reason was accepted';
    EXCEPTION WHEN check_violation THEN
        RAISE NOTICE '3. script_review rejected without reason blocked OK';
    END;

    -- 4. Test task_assignment role check
    INSERT INTO task (created_by, task_type, domain, target_qty, deadline)
    VALUES (v_manager, 'recording', 'it_technology', 10, now() + INTERVAL '5 days')
    RETURNING task_id INTO v_task_it;

    BEGIN
        INSERT INTO task_assignment (task_id, user_id, assignment_status)
        VALUES (v_task_it, v_reviewer, 'active');
        RAISE EXCEPTION '4. FAILED: recording task assigned to reviewer was accepted';
    EXCEPTION WHEN raise_exception THEN
        RAISE NOTICE '4. Recording task assigned to reviewer blocked OK';
    END;

    -- 5. Test task_script domain match & validated status
    INSERT INTO script (script_id, cs_content, vi_content, domain, status, created_by)
    VALUES ('s_121000001', 'School [EN] class [VI] lop', 'Lop hoc', 'education', 'validated', v_admin)
    ON CONFLICT (script_id) DO NOTHING;

    BEGIN
        INSERT INTO task_script (task_id, script_id)
        VALUES (v_task_it, 's_121000001');
        RAISE EXCEPTION '5a. FAILED: education script attached to IT task was accepted';
    EXCEPTION WHEN raise_exception THEN
        RAISE NOTICE '5a. Task-script domain mismatch blocked OK';
    END;

    -- 6. Test released dataset immutability
    INSERT INTO dataset (dataset_name, version, status)
    VALUES ('Test Dataset', 'v1.0', 'released')
    RETURNING dataset_id INTO v_dataset_id;

    BEGIN
        INSERT INTO dataset_recording (dataset_id, recording_id)
        VALUES (v_dataset_id, 'r_cs_111000001_t10');
        RAISE EXCEPTION '6. FAILED: insert into released dataset was accepted';
    EXCEPTION WHEN raise_exception THEN
        RAISE NOTICE '6. Insert into released dataset blocked OK';
    END;

    RAISE NOTICE 'All V6 verification tests passed successfully.';
END $$;

ROLLBACK;
