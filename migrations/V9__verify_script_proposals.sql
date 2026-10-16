-- ============================================================
-- V9 verification (psql): đề xuất câu vào hàng chờ Reviewer (TEAM_015).
--
-- Chạy trong transaction mà ROLLBACK ở cuối nên không để lại dữ liệu nào.
-- Run: psql -d codeswitchlabel -v ON_ERROR_STOP=1 -f migrations/V9__verify_script_proposals.sql
-- ============================================================
BEGIN;

-- ------------------------------------------------------------------
-- 1. Hai kiểu ENUM đúng giá trị, đúng thứ tự.
-- ------------------------------------------------------------------
DO $$
DECLARE
    v_kind   TEXT[];
    v_status TEXT[];
BEGIN
    SELECT array_agg(e.enumlabel ORDER BY e.enumsortorder) INTO v_kind
      FROM pg_enum e JOIN pg_type t ON t.oid = e.enumtypid
     WHERE t.typname = 'proposal_kind';

    SELECT array_agg(e.enumlabel ORDER BY e.enumsortorder) INTO v_status
      FROM pg_enum e JOIN pg_type t ON t.oid = e.enumtypid
     WHERE t.typname = 'proposal_status';

    IF v_kind IS DISTINCT FROM ARRAY['report','edit']::TEXT[] THEN
        RAISE EXCEPTION 'proposal_kind sai: %', v_kind;
    END IF;

    IF v_status IS DISTINCT FROM ARRAY['pending','accepted','rejected']::TEXT[] THEN
        RAISE EXCEPTION 'proposal_status sai: %', v_status;
    END IF;

    RAISE NOTICE 'OK  proposal_kind / proposal_status đúng giá trị';
END $$;

-- ------------------------------------------------------------------
-- 2. Bảng + index hàng chờ (index một phần, chỉ tính đề xuất đang chờ).
-- ------------------------------------------------------------------
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM information_schema.tables
                    WHERE table_schema = 'public' AND table_name = 'script_proposal') THEN
        RAISE EXCEPTION 'thiếu bảng script_proposal';
    END IF;

    IF NOT EXISTS (SELECT 1 FROM pg_indexes
                    WHERE tablename = 'script_proposal'
                      AND indexname = 'uq_script_proposal_pending'
                      AND indexdef LIKE '%WHERE (status = ''pending''%') THEN
        RAISE EXCEPTION 'uq_script_proposal_pending thiếu hoặc không phải index một phần';
    END IF;

    RAISE NOTICE 'OK  bảng script_proposal + index đề xuất chờ';
END $$;

-- ------------------------------------------------------------------
-- 3. Ba trigger tồn tại, đúng bảng.
-- ------------------------------------------------------------------
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_trigger tg JOIN pg_class c ON c.oid = tg.tgrelid
                    WHERE c.relname = 'script_proposal'
                      AND tg.tgname = 'trg_script_proposal_guard') THEN
        RAISE EXCEPTION 'thiếu trigger trg_script_proposal_guard';
    END IF;

    IF NOT EXISTS (SELECT 1 FROM pg_trigger tg JOIN pg_class c ON c.oid = tg.tgrelid
                    WHERE c.relname = 'script_proposal'
                      AND tg.tgname = 'trg_script_proposal_sync') THEN
        RAISE EXCEPTION 'thiếu trigger trg_script_proposal_sync';
    END IF;

    IF NOT EXISTS (SELECT 1 FROM pg_trigger tg JOIN pg_class c ON c.oid = tg.tgrelid
                    WHERE c.relname = 'script'
                      AND tg.tgname = 'trg_script_proposal_close_on_script') THEN
        RAISE EXCEPTION 'thiếu trigger trg_script_proposal_close_on_script trên bảng script';
    END IF;

    RAISE NOTICE 'OK  ba trigger đề xuất có mặt';
END $$;

-- ------------------------------------------------------------------
-- 4. Hành vi thật. Luôn ROLLBACK ở cuối nên phần thử dữ liệu vô hại;
--    khi database không có fixture phù hợp thì bỏ qua kèm NOTICE SKIP.
-- ------------------------------------------------------------------

-- 4a. Guard: câu CHƯA duyệt nội dung thì đề xuất phải bị chặn.
DO $$
DECLARE
    v_script   VARCHAR(11);
    v_user     BIGINT;
    v_inserted BOOLEAN := FALSE;
BEGIN
    SELECT s.script_id INTO v_script
      FROM script s
     WHERE s.status IN ('pending_validation','rejected','deactivated')
     LIMIT 1;

    SELECT u.user_id INTO v_user FROM app_user u LIMIT 1;

    IF v_script IS NULL OR v_user IS NULL THEN
        RAISE NOTICE 'SKIP  guard: không có câu chưa duyệt hoặc người dùng để thử';
        RETURN;
    END IF;

    BEGIN
        INSERT INTO script_proposal (script_id, proposer_id, kind, comment)
        VALUES (v_script, v_user, 'report', 'V9 verify — phải bị guard chặn');
        v_inserted := TRUE;
    EXCEPTION WHEN others THEN
        NULL;   -- bị chặn là đúng
    END;

    IF v_inserted THEN
        RAISE EXCEPTION 'V9 verify: guard đã KHÔNG chặn đề xuất trên câu chưa duyệt %', v_script;
    END IF;

    RAISE NOTICE 'OK  guard chặn đề xuất trên câu chưa duyệt';
END $$;

-- 4b. Trọn vòng một đề xuất trên câu validated chưa thu:
--     tạo đề xuất → rút câu khỏi hàng ghi âm; trùng đề xuất chờ → chặn;
--     chốt đề xuất → trả câu về hàng ghi âm.
DO $$
DECLARE
    v_script   VARCHAR(11);
    v_user     BIGINT;
    v_reason   SMALLINT;
    v_proposal BIGINT;
    v_open     BOOLEAN;
BEGIN
    SELECT s.script_id INTO v_script
      FROM script s
     WHERE s.status = 'validated'
       AND NOT EXISTS (SELECT 1 FROM recording r WHERE r.script_id = s.script_id)
     LIMIT 1;

    SELECT u.user_id INTO v_user FROM app_user u LIMIT 1;

    SELECT r.reason_id INTO v_reason
      FROM script_error_reason r WHERE r.is_active LIMIT 1;

    IF v_script IS NULL OR v_user IS NULL OR v_reason IS NULL THEN
        RAISE NOTICE 'SKIP  hành vi: không có câu validated chưa thu để thử';
        RETURN;
    END IF;

    -- Câu validated chưa thu luôn có hàng ghi âm đang mở (do trg_script_queue_sync).
    INSERT INTO script_queue (script_id, stage, state)
    VALUES (v_script, 'recording', 'queued')
    ON CONFLICT (script_id, stage) WHERE state IN ('queued','claimed')
    DO NOTHING;

    INSERT INTO script_proposal (script_id, proposer_id, kind, error_reason_id, comment)
    VALUES (v_script, v_user, 'report', v_reason, 'V9 verify')
    RETURNING proposal_id INTO v_proposal;

    SELECT EXISTS (SELECT 1 FROM script_queue
                    WHERE script_id = v_script AND stage = 'recording'
                      AND state IN ('queued','claimed')) INTO v_open;

    IF v_open THEN
        RAISE EXCEPTION 'V9 verify: đề xuất chờ đã KHÔNG rút câu khỏi hàng ghi âm';
    END IF;

    -- Đề xuất chờ thứ hai trên cùng câu phải bị unique_violation chặn.
    BEGIN
        INSERT INTO script_proposal (script_id, proposer_id, kind, error_reason_id)
        VALUES (v_script, v_user, 'report', v_reason);

        RAISE EXCEPTION 'V9 verify: đã cho tạo hai đề xuất chờ trên cùng một câu';
    EXCEPTION WHEN unique_violation THEN
        NULL;   -- đúng như mong đợi
    END;

    UPDATE script_proposal
       SET status = 'accepted', decided_at = now()
     WHERE proposal_id = v_proposal;

    SELECT EXISTS (SELECT 1 FROM script_queue
                    WHERE script_id = v_script AND stage = 'recording'
                      AND state IN ('queued','claimed')) INTO v_open;

    IF NOT v_open THEN
        RAISE EXCEPTION 'V9 verify: chốt đề xuất xong đã KHÔNG trả câu về hàng ghi âm';
    END IF;

    RAISE NOTICE 'OK  đề xuất chờ rút câu khỏi hàng ghi âm, chốt xong trả về';
END $$;

-- 4c. Câu bị loại khỏi kho → đề xuất đang chờ tự đóng.
DO $$
DECLARE
    v_script   VARCHAR(11);
    v_user     BIGINT;
    v_proposal BIGINT;
    v_status   proposal_status;
    v_note     TEXT;
BEGIN
    SELECT s.script_id INTO v_script
      FROM script s
     WHERE s.status = 'validated'
       AND NOT EXISTS (SELECT 1 FROM recording r WHERE r.script_id = s.script_id)
     LIMIT 1;

    SELECT u.user_id INTO v_user FROM app_user u LIMIT 1;

    IF v_script IS NULL OR v_user IS NULL THEN
        RAISE NOTICE 'SKIP  tự đóng: không có câu validated chưa thu để thử';
        RETURN;
    END IF;

    -- Đề xuất sửa hợp lệ trên câu đã duyệt...
    INSERT INTO script_proposal (script_id, proposer_id, kind, comment,
                                 proposed_cs_content, proposed_vi_content)
    VALUES (v_script, v_user, 'edit', 'V9 verify — câu bị loại thì đề xuất tự đóng',
            '[vi]câu đề xuất A', '[vi]câu gốc A')
    RETURNING proposal_id INTO v_proposal;

    -- ...rồi câu bị loại khỏi kho → đề xuất phải tự đóng kèm lý do.
    UPDATE script SET status = 'rejected' WHERE script_id = v_script;

    SELECT p.status, p.decision_note INTO v_status, v_note
      FROM script_proposal p WHERE p.proposal_id = v_proposal;

    IF v_status IS DISTINCT FROM 'rejected' OR v_note IS NULL THEN
        RAISE EXCEPTION 'V9 verify: đề xuất chưa tự đóng khi câu bị loại (status=%, note=%)', v_status, v_note;
    END IF;

    RAISE NOTICE 'OK  câu bị loại thì đề xuất đang chờ tự đóng';
END $$;

ROLLBACK;
