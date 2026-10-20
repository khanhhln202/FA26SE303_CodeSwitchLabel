-- ============================================================
-- V11 verification (psql): chốt kết quả duyệt ngay khi đa số đã chắc.
--
-- Chạy trong transaction mà ROLLBACK ở cuối nên không để lại dữ liệu nào.
-- Run: psql -d codeswitchlabel -v ON_ERROR_STOP=1 -f migrations/V11__verify_early_majority.sql
-- ============================================================
BEGIN;

-- ------------------------------------------------------------------
-- 1. Hai phiếu từ chối (còn thiếu lượt thứ ba) là bản ghi trượt NGAY.
--    Dựng đủ người, câu, bản ghi ngay trong transaction rồi xoá theo ROLLBACK.
-- ------------------------------------------------------------------
DO $$
DECLARE
    v_speaker  BIGINT;
    v_rv1      BIGINT;
    v_rv2      BIGINT;
    v_rv3      BIGINT;
    -- Mã câu mã hoá sẵn ba thứ (ck_script_id_word_digit / ck_script_id_domain_digit):
    --   chữ số 1 = số từ tiếng Anh (1), chữ số 2 = chủ đề (1 = it_technology),
    --   chữ số 3 = quan hệ Anh-Việt (1 = dịch thẳng). Dãy 999xxx để không đụng số thật.
    v_script   TEXT := 's_111999001';
    v_rec      TEXT := 'r_cs_111999001';
    v_status   recording_status;
    v_rounds   INT;
BEGIN
    SELECT COALESCE(NULLIF(config_value, '')::int, 3) INTO v_rounds
      FROM system_config WHERE config_key = 'review.rounds_required';

    IF v_rounds <> 3 THEN
        RAISE NOTICE 'review.rounds_required = % (không phải 3) — bỏ qua phần dựng dữ liệu.', v_rounds;
        RETURN;
    END IF;

    INSERT INTO app_user (role_id, full_name, email, password_hash, status)
    SELECT r.role_id, 'V11 speaker', 'v11-speaker@verify.local', 'x', 'active'
      FROM role r WHERE r.role_name = 'speaker'
    RETURNING user_id INTO v_speaker;

    INSERT INTO app_user (role_id, full_name, email, password_hash, status)
    SELECT r.role_id, 'V11 rv1', 'v11-rv1@verify.local', 'x', 'active'
      FROM role r WHERE r.role_name = 'reviewer'
    RETURNING user_id INTO v_rv1;

    INSERT INTO app_user (role_id, full_name, email, password_hash, status)
    SELECT r.role_id, 'V11 rv2', 'v11-rv2@verify.local', 'x', 'active'
      FROM role r WHERE r.role_name = 'reviewer'
    RETURNING user_id INTO v_rv2;

    INSERT INTO app_user (role_id, full_name, email, password_hash, status)
    SELECT r.role_id, 'V11 rv3', 'v11-rv3@verify.local', 'x', 'active'
      FROM role r WHERE r.role_name = 'reviewer'
    RETURNING user_id INTO v_rv3;

    -- Câu để ở 'pending_validation': nhánh trả câu về duyệt nội dung không đụng tới nó,
    -- nên phần kiểm này chỉ nói về trạng thái BẢN GHI.
    INSERT INTO script (script_id, cs_content, vi_content, status, word_count, en_word_count,
                        domain, created_by)
    VALUES (v_script, '[vi]Câu kiểm tra [en]verify [vi]V11.', '[vi]Câu kiểm tra V11.',
            'pending_validation', 5, 1, 'it_technology', v_speaker);

    INSERT INTO recording (recording_id, script_id, speaker_id, sentence_variant, duration_sec,
                           status, cloud_link, audio_format)
    VALUES (v_rec, v_script, v_speaker, 'code_switching', 3.0,
            'pending_review', 's3://verify/v11.wav', 'wav');

    INSERT INTO review (recording_id, reviewer_id, review_round, decision, is_blind)
    VALUES (v_rec, v_rv1, 1, 'rejected', TRUE);

    SELECT status INTO v_status FROM recording WHERE recording_id = v_rec;
    IF v_status <> 'pending_review' THEN
        RAISE EXCEPTION 'một phiếu chưa quá bán mà đã chốt: %', v_status;
    END IF;

    INSERT INTO review (recording_id, reviewer_id, review_round, decision, is_blind)
    VALUES (v_rec, v_rv2, 2, 'rejected', TRUE);

    SELECT status INTO v_status FROM recording WHERE recording_id = v_rec;
    IF v_status <> 'rejected' THEN
        RAISE EXCEPTION 'hai phiếu từ chối phải chốt trượt ngay, đang là: %', v_status;
    END IF;

    RAISE NOTICE 'OK — hai phiếu từ chối chốt trượt ngay, không chờ lượt thứ ba.';
END $$;

-- ------------------------------------------------------------------
-- 2. Không còn bản ghi nào đã quá bán mà vẫn "chờ duyệt".
-- ------------------------------------------------------------------
DO $$
DECLARE
    v_ket INT;
BEGIN
    SELECT COUNT(*) INTO v_ket FROM (
        SELECT r.recording_id
          FROM recording r JOIN review v ON v.recording_id = r.recording_id
         WHERE r.status = 'pending_review'
         GROUP BY r.recording_id
        HAVING COUNT(*) FILTER (WHERE v.decision = 'approved')
                 >= (SELECT (COALESCE(NULLIF(config_value, '')::int, 3) / 2) + 1
                       FROM system_config WHERE config_key = 'review.rounds_required')
            OR COUNT(*) FILTER (WHERE v.decision = 'rejected')
                 >= (SELECT (COALESCE(NULLIF(config_value, '')::int, 3) / 2) + 1
                       FROM system_config WHERE config_key = 'review.rounds_required')
    ) x;

    IF v_ket > 0 THEN
        RAISE EXCEPTION 'còn % bản ghi đã quá bán nhưng vẫn chờ duyệt', v_ket;
    END IF;

    RAISE NOTICE 'OK — không còn bản ghi nào đã quá bán mà còn chờ duyệt.';
END $$;

ROLLBACK;
