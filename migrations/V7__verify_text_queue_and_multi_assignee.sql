-- ============================================================
-- V7 verification (psql): shared text queue + multi-assignee tasks.
-- Runs in a transaction that ROLLS BACK, so it never pollutes data.
-- Run: psql -d codeswitchlabel -v ON_ERROR_STOP=1 -f migrations/V7__verify_text_queue_and_multi_assignee.sql
-- ============================================================
BEGIN;

DO $$
DECLARE
    v_admin      BIGINT;
    v_manager    BIGINT;
    v_speaker    BIGINT;
    v_speaker2   BIGINT;
    v_domain_rev BIGINT;   -- reviewer qualified for it_technology
    v_rev1       BIGINT;
    v_rev2       BIGINT;
    v_rev3       BIGINT;
    v_content_rsn SMALLINT;

    v_task       BIGINT;
    v_assigned   INT;

    v_script_a   VARCHAR(11) := 's_110000001';
    v_script_b   VARCHAR(11) := 's_110000002';
    v_script_c   VARCHAR(11) := 's_110000003';
    v_script_d   VARCHAR(11) := 's_110000004';

    v_claim1     VARCHAR;
    v_claim2     VARCHAR;
    v_claim3     VARCHAR;
    v_qid        BIGINT;
    v_state      queue_state;
    v_status_txt TEXT;   -- statuses outside script_queue are different enums
    v_n          INT;
BEGIN
    -- ------------------------------------------------------------------
    -- Fixture: one admin, one manager, two speakers, three reviewers,
    -- and a reviewer who is qualified for the it_technology domain.
    -- ------------------------------------------------------------------
    SELECT user_id INTO v_admin FROM app_user
     WHERE role_id = (SELECT role_id FROM role WHERE role_name = 'admin') LIMIT 1;
    SELECT user_id INTO v_manager FROM app_user
     WHERE role_id = (SELECT role_id FROM role WHERE role_name = 'task_manager') LIMIT 1;

    SELECT user_id INTO v_speaker FROM app_user
     WHERE role_id = (SELECT role_id FROM role WHERE role_name = 'speaker')
       AND status = 'active' ORDER BY user_id LIMIT 1;

    -- Two speakers are the whole point of V7 section 1, so make a second one
    -- if the database only ships with one.
    SELECT user_id INTO v_speaker2 FROM app_user
     WHERE role_id = (SELECT role_id FROM role WHERE role_name = 'speaker')
       AND status = 'active' AND user_id <> v_speaker ORDER BY user_id LIMIT 1;

    IF v_speaker2 IS NULL THEN
        INSERT INTO app_user (role_id, full_name, email, password_hash, status)
        VALUES ((SELECT role_id FROM role WHERE role_name = 'speaker'),
                'V7 verify speaker 2', 'v7_verify_speaker2@codeswitchlabel.local',
                'x', 'active')
        RETURNING user_id INTO v_speaker2;
    END IF;

    -- Three distinct reviewers: the majority trigger only fires on the third.
    SELECT user_id INTO v_rev1 FROM app_user
     WHERE role_id = (SELECT role_id FROM role WHERE role_name = 'reviewer')
       AND status = 'active' ORDER BY user_id LIMIT 1;
    SELECT user_id INTO v_rev2 FROM app_user
     WHERE role_id = (SELECT role_id FROM role WHERE role_name = 'reviewer')
       AND status = 'active' AND user_id <> v_rev1 ORDER BY user_id LIMIT 1;
    IF v_rev2 IS NULL THEN
        INSERT INTO app_user (role_id, full_name, email, password_hash, status)
        VALUES ((SELECT role_id FROM role WHERE role_name = 'reviewer'),
                'V7 verify reviewer 2', 'v7_verify_rev2@codeswitchlabel.local', 'x', 'active')
        RETURNING user_id INTO v_rev2;
    END IF;

    SELECT user_id INTO v_rev3 FROM app_user
     WHERE role_id = (SELECT role_id FROM role WHERE role_name = 'reviewer')
       AND status = 'active'
       AND user_id NOT IN (v_rev1, v_rev2) ORDER BY user_id LIMIT 1;

    IF v_rev3 IS NULL THEN
        INSERT INTO app_user (role_id, full_name, email, password_hash, status)
        VALUES ((SELECT role_id FROM role WHERE role_name = 'reviewer'),
                'V7 verify reviewer 3', 'v7_verify_rev3@codeswitchlabel.local', 'x', 'active')
        RETURNING user_id INTO v_rev3;
    END IF;

    SELECT u.user_id INTO v_domain_rev FROM app_user u
      JOIN user_domain ud ON ud.user_id = u.user_id
     WHERE ud.domain = 'it_technology'
       AND u.role_id = (SELECT role_id FROM role WHERE role_name = 'reviewer')
     ORDER BY u.user_id LIMIT 1;

    SELECT reason_id INTO v_content_rsn FROM rejection_reason
     WHERE category = 'content' ORDER BY reason_id LIMIT 1;

    IF v_admin IS NULL OR v_manager IS NULL OR v_speaker IS NULL OR v_speaker2 IS NULL THEN
        RAISE EXCEPTION 'need admin, manager and at least two speakers for verification';
    END IF;
    IF v_rev1 IS NULL OR v_rev2 IS NULL OR v_rev3 IS NULL THEN
        RAISE EXCEPTION 'need three reviewers for the majority check';
    END IF;
    IF v_domain_rev IS NULL THEN
        RAISE EXCEPTION 'need a reviewer qualified for it_technology (user_domain)';
    END IF;
    IF v_content_rsn IS NULL THEN
        RAISE EXCEPTION 'need a rejection_reason in the content category';
    END IF;

    -- ------------------------------------------------------------------
    -- 1. Objects the rest of the migration depends on
    -- ------------------------------------------------------------------
    IF to_regclass('public.script_queue') IS NULL THEN
        RAISE EXCEPTION '1. FAILED: table script_queue does not exist';
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_type WHERE typname = 'queue_stage') THEN
        RAISE EXCEPTION '1. FAILED: enum queue_stage does not exist';
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_type WHERE typname = 'queue_state') THEN
        RAISE EXCEPTION '1. FAILED: enum queue_state does not exist';
    END IF;
    RAISE NOTICE '1. script_queue + queue enums exist OK';

    -- ------------------------------------------------------------------
    -- 2. Trigger: a pending sentence lands in the text_review stage
    -- ------------------------------------------------------------------
    INSERT INTO script (script_id, cs_content, vi_content, domain, status, created_by, en_word_count, word_count)
    VALUES (v_script_a, 'V7 verify [EN] alpha [VI]', 'V7 verify al-pha', 'it_technology', 'pending_validation', v_admin, 1, 1);

    SELECT state INTO v_state FROM script_queue WHERE script_id = v_script_a AND stage = 'text_review';
    IF v_state IS DISTINCT FROM 'queued' THEN
        RAISE EXCEPTION '2. FAILED: inserting a pending sentence did not queue it for text review (state=%)', v_state;
    END IF;
    RAISE NOTICE '2. Trigger queues pending_validation into text_review OK';

    -- ------------------------------------------------------------------
    -- 3. Trigger: validating drops the review row and opens the recording
    --    stage. Validating is gated by trg_script_validated_domain, so the
    --    qualifying script_review has to exist first.
    -- ------------------------------------------------------------------
    INSERT INTO script_review (script_id, user_id, action, reviewed_at)
    VALUES (v_script_a, v_domain_rev, 'accepted', now());

    UPDATE script SET status = 'validated' WHERE script_id = v_script_a;

    SELECT state INTO v_state FROM script_queue
     WHERE script_id = v_script_a AND stage = 'text_review';
    IF v_state IS DISTINCT FROM 'dropped' THEN
        RAISE EXCEPTION '3a. FAILED: text_review row was not dropped on validate (state=%)', v_state;
    END IF;

    SELECT state INTO v_state FROM script_queue
     WHERE script_id = v_script_a AND stage = 'recording';
    IF v_state IS DISTINCT FROM 'queued' THEN
        RAISE EXCEPTION '3b. FAILED: validating did not open the recording stage (state=%)', v_state;
    END IF;
    RAISE NOTICE '3. validate moves the sentence from text_review to recording OK';

    -- ------------------------------------------------------------------
    -- 4. Trigger: rejecting or deactivating clears every open stage
    -- ------------------------------------------------------------------
    INSERT INTO script (script_id, cs_content, vi_content, domain, status, created_by, en_word_count, word_count)
    VALUES (v_script_b, 'V7 verify [EN] bravo [VI]', 'V7 verify bra-vo', 'it_technology', 'pending_validation', v_admin, 1, 1);

    UPDATE script SET status = 'rejected' WHERE script_id = v_script_b;

    SELECT count(*) INTO v_n FROM script_queue
     WHERE script_id = v_script_b AND state IN ('queued', 'claimed');
    IF v_n <> 0 THEN
        RAISE EXCEPTION '4. FAILED: rejecting left % open queue row(s)', v_n;
    END IF;
    RAISE NOTICE '4. reject clears the open queue rows OK';

    -- ------------------------------------------------------------------
    -- 5. Index swap: one-per-task is gone, one-per-user-per-task is in
    -- ------------------------------------------------------------------
    IF EXISTS (SELECT 1 FROM pg_indexes
                WHERE tablename = 'task_assignment' AND indexname = 'uq_task_assignment_active') THEN
        RAISE EXCEPTION '5a. FAILED: uq_task_assignment_active still exists (a task can only have one assignee)';
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_indexes
                   WHERE tablename = 'task_assignment' AND indexname = 'uq_task_assignment_active_user') THEN
        RAISE EXCEPTION '5b. FAILED: uq_task_assignment_active_user is missing';
    END IF;
    RAISE NOTICE '5. task_assignment unique index swapped OK';

    -- ------------------------------------------------------------------
    -- 6. A task accepts several people, but still only one assignment per
    --    person, and target_qty stays a team goal.
    -- ------------------------------------------------------------------
    INSERT INTO task (campaign_id, created_by, task_type, domain, target_qty, deadline, status)
    VALUES (NULL, v_manager, 'recording', 'it_technology', 10, now() + INTERVAL '7 days', 'open')
    RETURNING task_id INTO v_task;

    INSERT INTO task_assignment (task_id, user_id, assignment_status)
    VALUES (v_task, v_speaker, 'active');

    BEGIN
        INSERT INTO task_assignment (task_id, user_id, assignment_status)
        VALUES (v_task, v_speaker, 'active');
        RAISE EXCEPTION '6a. FAILED: the same user was assigned to the same task twice';
    EXCEPTION WHEN unique_violation THEN
        RAISE NOTICE '6a. Duplicate assignment of the same user blocked OK';
    END;

    INSERT INTO task_assignment (task_id, user_id, assignment_status)
    VALUES (v_task, v_speaker2, 'active');

    SELECT count(*) INTO v_n FROM task_assignment
     WHERE task_id = v_task AND assignment_status = 'active';
    IF v_n <> 2 THEN
        RAISE EXCEPTION '6b. FAILED: expected 2 active assignees, found %', v_n;
    END IF;
    RAISE NOTICE '6b. Task assigned to two speakers at once OK';

    -- The quota is shared, not per head: 10 stays 10 no matter how many join.
    SELECT assignee_count, target_qty INTO v_n, v_assigned FROM v_task_progress WHERE task_id = v_task;
    IF v_n <> 2 THEN
        RAISE EXCEPTION '6c. FAILED: v_task_progress.assignee_count = %, expected 2', v_n;
    END IF;
    IF v_assigned <> 10 THEN
        RAISE EXCEPTION '6d. FAILED: target_qty = %, expected it to stay 10 for the whole group', v_assigned;
    END IF;
    RAISE NOTICE '6c. v_task_progress exposes assignee_count and target is not multiplied OK';

    -- ------------------------------------------------------------------
    -- 7. Claiming is atomic: two calls, two different sentences
    -- ------------------------------------------------------------------
    INSERT INTO script (script_id, cs_content, vi_content, domain, status, created_by, en_word_count, word_count)
    VALUES (v_script_c, 'V7 verify [EN] charlie [VI]', 'V7 verify char-lie', 'it_technology', 'pending_validation', v_admin, 1, 1);
    INSERT INTO script (script_id, cs_content, vi_content, domain, status, created_by, en_word_count, word_count)
    VALUES (v_script_d, 'V7 verify [EN] delta [VI]', 'V7 verify del-ta', 'it_technology', 'pending_validation', v_admin, 1, 1);

    SELECT f.script_id INTO v_claim1 FROM fn_queue_claim_next(v_rev1, 'text_review') f;
    SELECT f.script_id INTO v_claim2 FROM fn_queue_claim_next(v_rev2, 'text_review') f;

    IF v_claim1 IS NULL OR v_claim2 IS NULL THEN
        RAISE EXCEPTION '7a. FAILED: nothing to claim';
    END IF;
    IF v_claim1 = v_claim2 THEN
        RAISE EXCEPTION '7b. FAILED: two reviewers were handed the same sentence %', v_claim1;
    END IF;
    RAISE NOTICE '7. Two reviewers claimed two different sentences OK (%) / (%)', v_claim1, v_claim2;

    -- A claim is held, not owned forever.
    SELECT state, claimed_by INTO v_state, v_qid
      FROM script_queue WHERE script_id = v_claim1 AND stage = 'text_review';
    IF v_state IS DISTINCT FROM 'claimed' OR v_qid IS DISTINCT FROM v_rev1 THEN
        RAISE EXCEPTION '7c. FAILED: claim was not recorded for the reviewer (state=%, by=%)', v_state, v_qid;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM script_queue
                    WHERE script_id = v_claim1 AND stage = 'text_review' AND claim_expires_at > now()) THEN
        RAISE EXCEPTION '7d. FAILED: claim_expires_at was not set, so an abandoned claim would stick forever';
    END IF;
    RAISE NOTICE '7. Claim is recorded with a TTL OK';

    -- ------------------------------------------------------------------
    -- 8. An expired claim goes back on the queue instead of vanishing
    -- ------------------------------------------------------------------
    UPDATE script_queue SET claim_expires_at = now() - INTERVAL '1 hour'
     WHERE script_id = v_claim1 AND stage = 'text_review' AND state = 'claimed';

    SELECT f.script_id INTO v_claim3 FROM fn_queue_claim_next(v_rev3, 'text_review') f;
    IF v_claim3 IS DISTINCT FROM v_claim1 THEN
        RAISE EXCEPTION '8. FAILED: an expired claim was not offered again (got %, expected %)',
            v_claim3, v_claim1;
    END IF;
    RAISE NOTICE '8. Expired claim became claimable again OK';

    -- ------------------------------------------------------------------
    -- 9. Release returns work to the queue; requeue also recovers a row
    --    that already reached 'done', which is what "delete audio only"
    --    depends on.
    -- ------------------------------------------------------------------
    v_n := fn_queue_release(v_claim2, 'text_review', 'queued', 'claimed');
    IF v_n <> 1 THEN
        RAISE EXCEPTION '9a. FAILED: fn_queue_release moved % rows, expected 1', v_n;
    END IF;
    SELECT state INTO v_state FROM script_queue WHERE script_id = v_claim2 AND stage = 'text_review';
    IF v_state IS DISTINCT FROM 'queued' THEN
        RAISE EXCEPTION '9b. FAILED: released row is %, expected queued', v_state;
    END IF;
    RAISE NOTICE '9a. Release returns a claimed row to queued OK';

    UPDATE script_queue SET state = 'done' WHERE script_id = v_claim2 AND stage = 'text_review';
    v_n := fn_queue_requeue(v_claim2, 'text_review');
    IF v_n <> 1 THEN
        RAISE EXCEPTION '9c. FAILED: fn_queue_requeue recovered % rows, expected 1', v_n;
    END IF;
    SELECT state INTO v_state FROM script_queue WHERE script_id = v_claim2 AND stage = 'text_review';
    IF v_state IS DISTINCT FROM 'queued' THEN
        RAISE EXCEPTION '9d. FAILED: requeued row is %, expected queued', v_state;
    END IF;
    RAISE NOTICE '9b. Requeue recovers a row that already reached done OK';

    -- ------------------------------------------------------------------
    -- 10. Discard trail lives on recording, not in review
    -- ------------------------------------------------------------------
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns
                    WHERE table_name = 'recording' AND column_name = 'discarded_at')
       OR NOT EXISTS (SELECT 1 FROM information_schema.columns
                    WHERE table_name = 'recording' AND column_name = 'discarded_by')
       OR NOT EXISTS (SELECT 1 FROM information_schema.columns
                    WHERE table_name = 'recording' AND column_name = 'discard_note') THEN
        RAISE EXCEPTION '10. FAILED: recording is missing the discard trail columns';
    END IF;
    RAISE NOTICE '10. recording carries discarded_at / discarded_by / discard_note OK';

    -- ------------------------------------------------------------------
    -- 11. The V7 hang fix: rejecting a recording for CONTENT now also
    --     marks the task_script rejected, so the task can actually finish
    --     instead of waiting forever for work that will never come.
    --
    --     ORDERING CAVEAT — read this before trusting the fix in the app.
    --     trg_review_majority is AFTER INSERT ON `review`, but EF writes
    --     `review` BEFORE its `review_rejection_reason` rows (the FK needs
    --     review_id first). So the trigger sees the reasons only if an EARLIER
    --     round of the SAME recording already carries a content reason.
    --     Round 1 gets its reason here, which is the order that works.
    --     Verified failing case: reasons attached only to the deciding round.
    -- ------------------------------------------------------------------
    INSERT INTO script (script_id, cs_content, vi_content, domain, status, created_by, en_word_count, word_count)
    VALUES ('s_110000005', 'V7 verify [EN] echo [VI]', 'V7 verify ech-o', 'it_technology', 'pending_validation', v_admin, 1, 1);
    INSERT INTO script_review (script_id, user_id, action, reviewed_at)
    VALUES ('s_110000005', v_domain_rev, 'accepted', now());
    UPDATE script SET status = 'validated' WHERE script_id = 's_110000005';

    INSERT INTO task_script (task_id, script_id, status)
    VALUES (v_task, 's_110000005', 'pending');

    INSERT INTO recording (recording_id, sentence_variant, script_id, speaker_id,
                           cloud_link, duration_sec, status)
    VALUES ('r_cs_110000005', 'code_switching', 's_110000005', v_speaker,
            'http://v7-verify/110000005', 5.0, 'pending_review');

    -- Round 1 rejects on content grounds and links its reason straight away.
    INSERT INTO review (recording_id, reviewer_id, review_round, decision, is_blind)
    VALUES ('r_cs_110000005', v_rev1, 1, 'rejected', TRUE)
    RETURNING review_id INTO v_qid;
    INSERT INTO review_rejection_reason (review_id, reason_id) VALUES (v_qid, v_content_rsn);

    -- Rounds 2 and 3 close the vote: three rejections out of three.
    INSERT INTO review (recording_id, reviewer_id, review_round, decision, is_blind)
    VALUES ('r_cs_110000005', v_rev2, 2, 'rejected', TRUE),
           ('r_cs_110000005', v_rev3, 3, 'rejected', TRUE);

    SELECT status::text INTO v_status_txt FROM recording WHERE recording_id = 'r_cs_110000005';
    IF v_status_txt <> 'rejected' THEN
        RAISE EXCEPTION '11a. FAILED: majority did not reject the recording (status=%)', v_status_txt;
    END IF;

    SELECT status::text INTO v_status_txt FROM task_script
     WHERE task_id = v_task AND script_id = 's_110000005';
    IF v_status_txt <> 'rejected' THEN
        RAISE EXCEPTION '11b. FAILED: task_script is still %, so the task hangs below target forever', v_status_txt;
    END IF;

    SELECT status::text INTO v_status_txt FROM script WHERE script_id = 's_110000005';
    IF v_status_txt <> 'pending_validation' THEN
        RAISE EXCEPTION '11c. FAILED: script is % after a content rejection, expected it back in review', v_status_txt;
    END IF;
    RAISE NOTICE '11. Content rejection frees the slot: task_script rejected, sentence back to review OK';

    -- ------------------------------------------------------------------
    -- 12. New knobs exist
    -- ------------------------------------------------------------------
    IF NOT EXISTS (SELECT 1 FROM system_config WHERE config_key = 'queue.claim_ttl_minutes') THEN
        RAISE EXCEPTION '12. FAILED: system_config.queue.claim_ttl_minutes is missing';
    END IF;
    IF NOT EXISTS (SELECT 1 FROM system_config WHERE config_key = 'text_review.batch_size') THEN
        RAISE EXCEPTION '12. FAILED: system_config.text_review.batch_size is missing';
    END IF;
    RAISE NOTICE '12. queue.claim_ttl_minutes and text_review.batch_size seeded OK';

    RAISE NOTICE 'ALL V7 CHECKS PASSED';
END $$;

ROLLBACK;