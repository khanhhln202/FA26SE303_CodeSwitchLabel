-- ============================================================
-- V18 verification (psql): bản ghi bị từ chối không còn đẩy câu về duyệt nội dung.
--
-- Chạy trong transaction mà ROLLBACK ở cuối nên không để lại dữ liệu nào.
-- Run: psql -d codeswitchlabel -v ON_ERROR_STOP=1 -f migrations/V18__verify_rejection_keeps_script.sql
-- ============================================================
BEGIN;

-- ------------------------------------------------------------------
-- 1. Thân hàm không còn nhánh đẩy câu về pending_validation.
-- ------------------------------------------------------------------
DO $$
BEGIN
    IF EXISTS (
        SELECT 1 FROM pg_proc
         WHERE proname = 'fn_review_majority_status'
           AND prosrc LIKE '%pending_validation%'
    ) THEN
        RAISE EXCEPTION 'fn_review_majority_status vẫn còn nhánh đẩy câu về duyệt nội dung';
    END IF;

    RAISE NOTICE 'OK — hàm chốt đa số không còn đụng tới trạng thái của câu.';
END $$;

-- ------------------------------------------------------------------
-- 2. Hai phiếu từ chối với lý do nhóm 'content' vẫn để câu ở 'validated'.
-- ------------------------------------------------------------------
DO $$
DECLARE
    v_speaker  BIGINT;
    v_rv1      BIGINT;
    v_rv2      BIGINT;
    v_reason   SMALLINT;
    v_review   BIGINT;
    v_script   TEXT := 's_111999002';
    v_rec      TEXT := 'r_cs_111999002';
    v_status   script_status;
    v_rounds   INT;
BEGIN
    SELECT COALESCE(NULLIF(config_value, '')::int, 3) INTO v_rounds
      FROM system_config WHERE config_key = 'review.rounds_required';

    IF v_rounds <> 3 THEN
        RAISE NOTICE 'review.rounds_required = % (không phải 3) — bỏ qua phần dựng dữ liệu.', v_rounds;
        RETURN;
    END IF;

    SELECT reason_id INTO v_reason
      FROM rejection_reason WHERE category = 'content' AND is_active LIMIT 1;

    IF v_reason IS NULL THEN
        RAISE EXCEPTION 'không còn lý do từ chối nào thuộc nhóm content để kiểm';
    END IF;

    INSERT INTO app_user (role_id, full_name, email, password_hash, status)
    SELECT r.role_id, 'V18 speaker', 'v18-speaker@verify.local', 'x', 'active'
      FROM role r WHERE r.role_name = 'speaker'
    RETURNING user_id INTO v_speaker;

    INSERT INTO app_user (role_id, full_name, email, password_hash, status)
    SELECT r.role_id, 'V18 rv1', 'v18-rv1@verify.local', 'x', 'active'
      FROM role r WHERE r.role_name = 'reviewer'
    RETURNING user_id INTO v_rv1;

    INSERT INTO app_user (role_id, full_name, email, password_hash, status)
    SELECT r.role_id, 'V18 rv2', 'v18-rv2@verify.local', 'x', 'active'
      FROM role r WHERE r.role_name = 'reviewer'
    RETURNING user_id INTO v_rv2;

    -- Câu để thẳng ở 'validated': phần kiểm này nói về chuyện câu CÓ BỊ ĐẨY ĐI hay không,
    -- không phải về đường đi tới validated.
    INSERT INTO script (script_id, cs_content, vi_content, status, word_count, en_word_count,
                        domain, created_by)
    VALUES (v_script, '[vi]Câu kiểm tra [en]verify [vi]V18.', '[vi]Câu kiểm tra V18.',
            'pending_validation', 5, 1, 'it_technology', v_speaker);

    INSERT INTO recording (recording_id, script_id, speaker_id, sentence_variant, duration_sec,
                           status, cloud_link, audio_format)
    VALUES (v_rec, v_script, v_speaker, 'code_switching', 3.0,
            'pending_review', 's3://verify/v18.wav', 'wav');

    UPDATE script SET status = 'validated' WHERE script_id = v_script;

    INSERT INTO review (recording_id, reviewer_id, review_round, decision, is_blind)
    VALUES (v_rec, v_rv1, 1, 'rejected', TRUE)
    RETURNING review_id INTO v_review;
    INSERT INTO review_rejection_reason (review_id, reason_id) VALUES (v_review, v_reason);

    INSERT INTO review (recording_id, reviewer_id, review_round, decision, is_blind)
    VALUES (v_rec, v_rv2, 2, 'rejected', TRUE)
    RETURNING review_id INTO v_review;
    INSERT INTO review_rejection_reason (review_id, reason_id) VALUES (v_review, v_reason);

    IF (SELECT status FROM recording WHERE recording_id = v_rec) <> 'rejected' THEN
        RAISE EXCEPTION 'hai phiếu từ chối phải chốt bản ghi là trượt';
    END IF;

    SELECT status INTO v_status FROM script WHERE script_id = v_script;

    IF v_status <> 'validated' THEN
        RAISE EXCEPTION 'câu phải giữ nguyên validated, đang là %', v_status;
    END IF;

    RAISE NOTICE 'OK — bản ghi trượt vì lý do nội dung, câu vẫn validated nên thu lại được ngay.';
END $$;

ROLLBACK;
