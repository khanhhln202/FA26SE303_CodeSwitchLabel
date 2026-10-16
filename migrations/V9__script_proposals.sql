-- ============================================================
-- V9 — Đề xuất câu (báo lỗi / đề xuất sửa) vào hàng chờ Reviewer (TEAM_015)
--
-- Thêm:
--   * hai kiểu ENUM proposal_kind / proposal_status
--   * bảng script_proposal — mỗi câu tối đa MỘT đề xuất chờ
--   * ba trigger giữ bất biến:
--       fn_script_proposal_guard           — câu phải validated, chưa có bản ghi âm
--       fn_script_proposal_sync            — đề xuất chờ giữ câu khỏi hàng đợi ghi âm;
--                                            xử lý xong trả câu về hàng đợi
--       fn_script_proposal_close_on_script — câu bị loại khỏi kho thì đề xuất tự đóng
--
-- Cài trên database ĐANG CHẠY (không dùng `docker compose down -v`):
--   cat migrations/V9__script_proposals.sql \
--     | docker exec -i csl-postgres psql -U csl -d codeswitchlabel -v ON_ERROR_STOP=1
--   cat migrations/V9__verify_script_proposals.sql \
--     | docker exec -i csl-postgres psql -U csl -d codeswitchlabel -v ON_ERROR_STOP=1
--
-- Idempotent: chạy lại nhiều lần vẫn cho kết quả như nhau.
--
-- Không có BEGIN/COMMIT bao ngoài (cùng lý do với V8): chạy lại sau khi đã xong
-- một phần phải tiếp tục được từ chỗ dở. Tất cả câu lệnh dưới đây đều tự bỏ qua
-- khi phần việc tương ứng đã xong.
-- ============================================================

-- ------------------------------------------------------------
-- 1. Kiểu ENUM mới. CREATE TYPE không có IF NOT EXISTS nên phải hỏi pg_type.
-- ------------------------------------------------------------
DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_type WHERE typname = 'proposal_kind') THEN
        CREATE TYPE proposal_kind AS ENUM ('report','edit');
    END IF;

    IF NOT EXISTS (SELECT 1 FROM pg_type WHERE typname = 'proposal_status') THEN
        CREATE TYPE proposal_status AS ENUM ('pending','accepted','rejected');
    END IF;
END $$;

-- ------------------------------------------------------------
-- 2. Bảng đề xuất — NGUYÊN VĂN theo docs/codeswitchlabel.sql.
--    Chỉ câu đã duyệt nội dung và chưa có bản ghi âm mới nhận đề xuất.
-- ------------------------------------------------------------
CREATE TABLE IF NOT EXISTS script_proposal (
    proposal_id         BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    script_id           VARCHAR(11) NOT NULL REFERENCES script(script_id),
    proposer_id         BIGINT NOT NULL REFERENCES app_user(user_id),
    kind                proposal_kind NOT NULL,

    error_reason_id     SMALLINT REFERENCES script_error_reason(reason_id),
    comment             TEXT,

    proposed_cs_content TEXT,
    proposed_vi_content TEXT,
    proposed_alignment  JSONB,

    status              proposal_status NOT NULL DEFAULT 'pending',
    decided_by          BIGINT REFERENCES app_user(user_id),
    decided_at          TIMESTAMPTZ,
    decision_note       TEXT,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT now(),

    CONSTRAINT ck_script_proposal_report_reason CHECK (kind <> 'report'
        OR error_reason_id IS NOT NULL),
    CONSTRAINT ck_script_proposal_edit_pair CHECK (kind <> 'edit'
        OR (proposed_cs_content IS NOT NULL AND proposed_vi_content IS NOT NULL)),
    CONSTRAINT ck_script_proposal_decision CHECK ((status = 'pending') = (decided_at IS NULL))
);

CREATE UNIQUE INDEX IF NOT EXISTS uq_script_proposal_pending
    ON script_proposal (script_id) WHERE status = 'pending';
CREATE INDEX IF NOT EXISTS idx_script_proposal_queue
    ON script_proposal (created_at) WHERE status = 'pending';

-- ------------------------------------------------------------
-- 3. Trigger — NGUYÊN VĂN theo docs/codeswitchlabel.sql.
-- ------------------------------------------------------------

-- 3a. Câu nhận đề xuất phải đang chờ thu: validated, chưa có bản ghi âm nào.
CREATE OR REPLACE FUNCTION fn_script_proposal_guard() RETURNS trigger AS $$
DECLARE
    v_status script_status;
BEGIN
    SELECT status INTO v_status FROM script WHERE script_id = NEW.script_id;

    IF v_status IS DISTINCT FROM 'validated' THEN
        RAISE EXCEPTION 'proposal: script % must be validated (currently %)', NEW.script_id, v_status;
    END IF;

    IF EXISTS (SELECT 1 FROM recording WHERE script_id = NEW.script_id) THEN
        RAISE EXCEPTION 'proposal: script % already has recordings', NEW.script_id;
    END IF;

    RETURN NEW;
END $$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_script_proposal_guard ON script_proposal;
CREATE TRIGGER trg_script_proposal_guard
    BEFORE INSERT ON script_proposal
    FOR EACH ROW EXECUTE FUNCTION fn_script_proposal_guard();

-- 3b. Đề xuất chờ giữ câu khỏi hàng đợi ghi âm; xử lý xong trả câu về hàng đợi.
CREATE OR REPLACE FUNCTION fn_script_proposal_sync() RETURNS trigger AS $$ BEGIN
    IF TG_OP = 'INSERT' THEN
        UPDATE script_queue
           SET state = 'dropped'
         WHERE script_id = NEW.script_id
           AND stage = 'recording'
           AND state IN ('queued','claimed');

    ELSIF OLD.status = 'pending' AND NEW.status IN ('accepted','rejected') THEN
        IF EXISTS (SELECT 1 FROM script s
                    WHERE s.script_id = NEW.script_id AND s.status = 'validated')
           AND NOT EXISTS (SELECT 1 FROM recording r WHERE r.script_id = NEW.script_id) THEN
            INSERT INTO script_queue (script_id, stage, state)
            VALUES (NEW.script_id, 'recording', 'queued')
            ON CONFLICT (script_id, stage) WHERE state IN ('queued','claimed')
            DO NOTHING;
        END IF;
    END IF;

    RETURN NEW;
END $$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_script_proposal_sync ON script_proposal;
CREATE TRIGGER trg_script_proposal_sync
    AFTER INSERT OR UPDATE OF status ON script_proposal
    FOR EACH ROW EXECUTE FUNCTION fn_script_proposal_sync();

-- 3c. Câu bị loại khỏi kho thì đề xuất đang chờ của nó tự đóng.
CREATE OR REPLACE FUNCTION fn_script_proposal_close_on_script() RETURNS trigger AS $$ BEGIN
    IF NEW.status IN ('rejected','deactivated') AND OLD.status IS DISTINCT FROM NEW.status THEN
        UPDATE script_proposal
           SET status = 'rejected',
               decided_at = now(),
               decision_note = 'Câu đã bị loại khỏi kho nên đề xuất tự đóng.'
         WHERE script_id = NEW.script_id AND status = 'pending';
    END IF;

    RETURN NEW;
END $$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_script_proposal_close_on_script ON script;
CREATE TRIGGER trg_script_proposal_close_on_script
    AFTER UPDATE OF status ON script
    FOR EACH ROW EXECUTE FUNCTION fn_script_proposal_close_on_script();
