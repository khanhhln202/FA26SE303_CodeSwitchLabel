-- ============================================================================
-- TEAM_010 — Hàng đợi văn bản chung, lượt duyệt của hệ thống, đa người nhận task
--
-- Cài trên database ĐANG CHẠY (không dùng `docker compose down -v`):
--   cat migrations/V7__text_queue_and_multi_assignee.sql \
--     | docker exec -i csl-postgres psql -U csl -d codeswitchlabel -v ON_ERROR_STOP=1
--   cat migrations/V7__verify_text_queue_and_multi_assignee.sql \
--     | docker exec -i csl-postgres psql -U csl -d codeswitchlabel -v ON_ERROR_STOP=1
--
-- Thứ tự quan trọng: đổi cột review.reviewer_id thành nullable TRƯỚC khi tạo
-- index unique mới, vì index cũ UNIQUE(recording_id, reviewer_id) không dùng
-- được với NULL.
-- ============================================================================

BEGIN;

-- ---------------------------------------------------------------------------
-- 1. Nhiều người nhận một task
-- ---------------------------------------------------------------------------
-- Index cũ chặn cứng: mỗi task chỉ một lượt giao active. Thay bằng ràng buộc
-- đúng nghĩa mới — cùng một người không được nhận trùng, nhưng task thì nhận
-- được bao nhiêu người tuỳ ý. target_qty là của CẢ NHÓM nên quota đợt vẫn
-- cộng đúng một lần (fn_validate_campaign_task_target không đụng index này).
DROP INDEX IF EXISTS uq_task_assignment_active;

CREATE UNIQUE INDEX uq_task_assignment_active_user
    ON task_assignment (task_id, user_id) WHERE assignment_status = 'active';

-- ---------------------------------------------------------------------------
-- 2. Kiểu enum mới
-- ---------------------------------------------------------------------------
CREATE TYPE queue_stage  AS ENUM ('text_review','recording');
CREATE TYPE queue_state  AS ENUM ('queued','claimed','done','dropped');

-- ---------------------------------------------------------------------------
-- 3. Bảng hàng đợi chung
-- ---------------------------------------------------------------------------
-- Câu đi hai chặng: text_review (duyệt nội dung) -> recording (ghi âm).
-- Hàng do trigger sinh, không phải do code, để không đường ghi nào lọt câu.
CREATE TABLE script_queue (
    queue_id         BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    script_id        VARCHAR(11) NOT NULL REFERENCES script(script_id) ON DELETE CASCADE,
    stage            queue_stage NOT NULL,
    enqueued_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
    state            queue_state NOT NULL DEFAULT 'queued',
    claimed_by       BIGINT REFERENCES app_user(user_id) ON DELETE SET NULL,
    claimed_at       TIMESTAMPTZ,
    claim_expires_at TIMESTAMPTZ,
    task_id          BIGINT REFERENCES task(task_id) ON DELETE CASCADE,
    CONSTRAINT ck_script_queue_claim CHECK (
        (state = 'claimed' AND claimed_by IS NOT NULL AND claim_expires_at IS NOT NULL) OR
        state <> 'claimed')
);

CREATE INDEX idx_script_queue_pick
    ON script_queue (stage, state, enqueued_at) WHERE state IN ('queued','claimed');
CREATE INDEX idx_script_queue_script ON script_queue (script_id, stage);
CREATE INDEX idx_script_queue_claim_expiry
    ON script_queue (claim_expires_at) WHERE state = 'claimed';
CREATE INDEX idx_script_queue_task ON script_queue (task_id) WHERE task_id IS NOT NULL;
CREATE UNIQUE INDEX uq_script_queue_open
    ON script_queue (script_id, stage) WHERE state IN ('queued','claimed');

-- ---------------------------------------------------------------------------
-- 4. Trigger giữ hàng đợi khớp với trạng thái câu
-- ---------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION fn_script_queue_sync() RETURNS trigger AS $$ BEGIN
    IF NEW.status = 'pending_validation' THEN
        INSERT INTO script_queue (script_id, stage, state)
        VALUES (NEW.script_id, 'text_review', 'queued')
        ON CONFLICT (script_id, stage) WHERE state IN ('queued','claimed')
        DO NOTHING;

    ELSIF NEW.status = 'validated' THEN
        UPDATE script_queue SET state = 'dropped'
         WHERE script_id = NEW.script_id AND stage = 'text_review'
           AND state IN ('queued','claimed');

        INSERT INTO script_queue (script_id, stage, state)
        VALUES (NEW.script_id, 'recording', 'queued')
        ON CONFLICT (script_id, stage) WHERE state IN ('queued','claimed')
        DO NOTHING;

    ELSIF NEW.status IN ('rejected', 'deactivated') THEN
        UPDATE script_queue SET state = 'dropped'
         WHERE script_id = NEW.script_id AND state IN ('queued','claimed');
    END IF;

    RETURN NEW;
END $$ LANGUAGE plpgsql;

CREATE TRIGGER trg_script_queue_sync
    AFTER INSERT OR UPDATE OF status ON script
    FOR EACH ROW EXECUTE FUNCTION fn_script_queue_sync();

-- Đưa các câu đang chờ sẵn vào hàng đợi (kho có dữ liệu sẵn từ trước migration).
-- Câu nào đã validated đẩy thẳng sang chặng ghi âm; câu pending sang duyệt nội dung.
INSERT INTO script_queue (script_id, stage, state, enqueued_at)
SELECT s.script_id,
       CASE WHEN s.status = 'pending_validation' THEN 'text_review'::queue_stage
            WHEN s.status = 'validated'           THEN 'recording'::queue_stage END,
       'queued', s.created_at
  FROM script s
 WHERE s.status IN ('pending_validation', 'validated')
ON CONFLICT (script_id, stage) WHERE state IN ('queued','claimed') DO NOTHING;

-- ---------------------------------------------------------------------------
-- 5. Claim atomic — hai người không thể nhận cùng một câu (yêu cầu 3.2)
-- ---------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION fn_queue_claim_next(
    p_user_id     BIGINT,
    p_stage       queue_stage,
    p_task_id     BIGINT DEFAULT NULL,
    p_ttl_minutes INT  DEFAULT NULL
) RETURNS TABLE (
    script_id VARCHAR, queue_id BIGINT,
    enqueued_at TIMESTAMPTZ, claim_expires_at TIMESTAMPTZ
) AS $$
DECLARE
    v_ttl INT;
    v_row script_queue%ROWTYPE;
BEGIN
    SELECT COALESCE(NULLIF(config_value, '')::int, 30) INTO v_ttl
      FROM system_config WHERE config_key = 'queue.claim_ttl_minutes';
    IF v_ttl IS NULL OR v_ttl < 1 THEN v_ttl := 30; END IF;
    v_ttl := COALESCE(p_ttl_minutes, v_ttl);

    SELECT q.* INTO v_row
      FROM script_queue q
      JOIN script s ON s.script_id = q.script_id
     WHERE q.stage = p_stage
       AND s.status = CASE q.stage
                         WHEN 'text_review' THEN 'pending_validation'::script_status
                         ELSE 'validated'::script_status END
       AND (q.state = 'queued' OR (q.state = 'claimed' AND q.claim_expires_at <= now()))
       -- Chặng nào cũng phải tôn trọng p_task_id, nhưng cách "thuộc task" khác nhau:
       --   duyệt văn bản -> task gắn thẳng vào hàng (q.task_id), theo lô của Task Manager.
       --   ghi âm       -> task gắn qua bảng trung gian task_script.
       AND (
            p_task_id IS NULL
            OR CASE q.stage
                 WHEN 'text_review' THEN (
                     q.task_id IS NULL
                     OR q.task_id = p_task_id
                     OR EXISTS (SELECT 1 FROM task_assignment a
                                 WHERE a.task_id = q.task_id
                                   AND a.user_id = p_user_id
                                   AND a.assignment_status = 'active')
                 )
                 ELSE EXISTS (SELECT 1 FROM task_script ts
                               WHERE ts.script_id = q.script_id
                                 AND ts.task_id = p_task_id
                                 AND ts.status = 'pending')
               END
           )
     ORDER BY q.enqueued_at, q.queue_id
     FOR UPDATE OF q SKIP LOCKED
     LIMIT 1;

    IF NOT FOUND THEN RETURN; END IF;

    UPDATE script_queue
       SET state = 'claimed', claimed_by = p_user_id, claimed_at = now(),
           claim_expires_at = now() + make_interval(mins => v_ttl)
     WHERE script_queue.queue_id = v_row.queue_id;

    RETURN QUERY SELECT v_row.script_id, v_row.queue_id, v_row.enqueued_at,
                        now() + make_interval(mins => v_ttl);
END $$ LANGUAGE plpgsql;

-- Chia lô câu văn bản cho Task Manager giao (yêu cầu 2.2 — mặc định 100 câu/người).
CREATE OR REPLACE FUNCTION fn_queue_claim_batch(
    p_task_id BIGINT,
    p_user_id BIGINT,
    p_count   INT,
    p_stage   queue_stage DEFAULT 'text_review'
) RETURNS SETOF VARCHAR AS $$
DECLARE
    v_claimed VARCHAR;
BEGIN
    FOR i IN 1..p_count LOOP
        SELECT c.script_id INTO v_claimed
          FROM fn_queue_claim_next(p_user_id, p_stage, p_task_id) c;
        EXIT WHEN v_claimed IS NULL;

        UPDATE script_queue SET task_id = p_task_id
         WHERE script_id = v_claimed AND stage = p_stage AND claimed_by = p_user_id;

        RETURN NEXT v_claimed;
    END LOOP;
END $$ LANGUAGE plpgsql;

-- Đóng claim: xong việc thì 'done', trả lại hàng đợi thì 'queued'.
-- p_from cho biết được phép chuyển TỪ trạng thái nào (mặc định 'claimed').
-- p_requeue_at_end đẩy câu xuống CUỐI hàng đợi — bắt buộc cho trường hợp "lấy được
-- nhưng không dùng được", nếu không câu đó vẫn đứng đầu và bị claim lại ngay.
CREATE OR REPLACE FUNCTION fn_queue_release(
    p_script_id VARCHAR,
    p_stage     queue_stage,
    p_state     queue_state DEFAULT 'queued',
    p_from      queue_state DEFAULT 'claimed',
    p_requeue_at_end BOOLEAN DEFAULT FALSE
) RETURNS INT AS $$
DECLARE v_count INT;
BEGIN
    UPDATE script_queue
       SET state = p_state,
           enqueued_at = CASE WHEN p_requeue_at_end THEN now() ELSE enqueued_at END,
           claimed_by = NULL, claimed_at = NULL, claim_expires_at = NULL
     WHERE script_id = p_script_id AND stage = p_stage AND state = p_from;
    GET DIAGNOSTICS v_count = ROW_COUNT;
    RETURN v_count;
END $$ LANGUAGE plpgsql;

-- Đưa câu trở lại hàng đợi sau khi bản ghi bị loại (hàng cũ đang 'done').
CREATE OR REPLACE FUNCTION fn_queue_requeue(
    p_script_id VARCHAR,
    p_stage     queue_stage DEFAULT 'recording'
) RETURNS INT AS $$
DECLARE v_moved INT;
BEGIN
    v_moved := fn_queue_release(p_script_id, p_stage, 'queued', 'done', TRUE);
    IF v_moved = 0 THEN
        v_moved := fn_queue_release(p_script_id, p_stage, 'queued', 'claimed', TRUE);
    END IF;
    IF v_moved = 0 THEN
        INSERT INTO script_queue (script_id, stage, state)
        VALUES (p_script_id, p_stage, 'queued')
        ON CONFLICT (script_id, stage) WHERE state IN ('queued','claimed') DO NOTHING;
        v_moved := 1;
    END IF;
    RETURN v_moved;
END $$ LANGUAGE plpgsql;

-- ---------------------------------------------------------------------------
-- 6. Bảng review KHÔNG đổi trong V7
-- ---------------------------------------------------------------------------
-- TEAM_010 từng chuẩn bị một lượt duyệt của hệ thống (reviewer_kind, reviewer_id
-- cho phép NULL). ĐÃ BỎ ở auto-QC chạy lúc nộp bản ghi và bản trượt thì
-- qc_failed, nên tới vòng duyệt chỉ còn bản đạt — lượt hệ thống không bao giờ có
-- tác dụng. Giữ 3 người duyệt, chốt đa số 2/3 như hệ thống cũ.

-- ---------------------------------------------------------------------------
-- 7. Bản ghi bị từ chối vì lý do 'content' thì câu mất mục công việc luôn
-- ---------------------------------------------------------------------------
-- Trước đây trigger trả câu về pending_validation nhưng để nguyên task_script ở
-- 'pending', nên task treo vĩnh viễn và không bao giờ đạt chỉ tiêu.
CREATE OR REPLACE FUNCTION fn_review_majority_status() RETURNS trigger AS $$ DECLARE
    v_rounds_required INT;
    v_total           INT;
    v_approved        INT;
    v_final           recording_status;
BEGIN
    SELECT COALESCE(NULLIF(config_value, '')::int, 3) INTO v_rounds_required
      FROM system_config WHERE config_key = 'review.rounds_required';
    IF v_rounds_required IS NULL OR v_rounds_required < 1 THEN v_rounds_required := 3; END IF;

    SELECT COUNT(*), COUNT(*) FILTER (WHERE decision = 'approved')
      INTO v_total, v_approved
      FROM review WHERE recording_id = NEW.recording_id;

    IF v_total >= v_rounds_required THEN
        v_final := CASE WHEN v_approved >= (v_rounds_required / 2) + 1 THEN 'approved' ELSE 'rejected' END;
        UPDATE recording SET status = v_final
         WHERE recording_id = NEW.recording_id AND status = 'pending_review';

        IF v_final = 'rejected' THEN
            IF EXISTS (
                SELECT 1 FROM review_rejection_reason rrr
                  JOIN rejection_reason rr ON rr.reason_id = rrr.reason_id
                  JOIN review rv ON rv.review_id = rrr.review_id
                 WHERE rv.recording_id = NEW.recording_id AND rr.category = 'content'
            ) THEN
                UPDATE script SET status = 'pending_validation'
                 WHERE script_id = (SELECT script_id FROM recording WHERE recording_id = NEW.recording_id)
                   AND status = 'validated';

                UPDATE task_script SET status = 'rejected'
                 WHERE script_id = (SELECT script_id FROM recording WHERE recording_id = NEW.recording_id)
                   AND status = 'pending';
            END IF;
        END IF;
    END IF;

    RETURN NEW;
END $$ LANGUAGE plpgsql;

-- ---------------------------------------------------------------------------
-- 8. Tham số mới
-- ---------------------------------------------------------------------------
INSERT INTO system_config (config_key, config_value, value_type, description) VALUES
    ('queue.claim_ttl_minutes', '30',  'int',
     'Minutes before an unclaimed queue item becomes claimable again'),
    ('text_review.batch_size',  '100', 'int',
     'Default number of pending sentences claimed per reviewer')
ON CONFLICT (config_key) DO NOTHING;

-- ---------------------------------------------------------------------------
-- 9. Dấu vết xử lý bản ghi (yêu cầu 2.4)
-- ---------------------------------------------------------------------------
-- Nút "Chỉ xóa audio" và "Xóa hoàn toàn" là quyết định dứt điểm, không phải phiếu
-- bầu — nên lưu riêng thay vì ghi vào bảng review (ghi vào đó sẽ làm lệch bộ đếm
-- 3 lượt và kích hoạt trigger chốt đa số một cách ngoài ý muốn).
ALTER TABLE recording
    ADD COLUMN IF NOT EXISTS discarded_at  TIMESTAMPTZ,
    ADD COLUMN IF NOT EXISTS discarded_by  BIGINT REFERENCES app_user(user_id),
    ADD COLUMN IF NOT EXISTS discard_note   TEXT;

-- ---------------------------------------------------------------------------
-- 10. View có assignee_count (một task có thể có nhiều người nhận)
-- ---------------------------------------------------------------------------
DROP VIEW IF EXISTS v_task_progress;
CREATE VIEW v_task_progress AS
SELECT
    t.task_id, t.campaign_id, c.campaign_name, t.task_type, t.domain,
    t.status AS task_status, t.target_qty, t.deadline,
    (SELECT COUNT(*) FROM task_assignment a
        WHERE a.task_id = t.task_id AND a.assignment_status = 'active') AS assignee_count,
    (SELECT COUNT(*) FROM task_script ts WHERE ts.task_id = t.task_id) AS scripts_assigned,
    (SELECT COUNT(*) FROM task_script ts
        WHERE ts.task_id = t.task_id AND ts.status = 'completed')      AS scripts_completed,
    (SELECT COUNT(*) FROM review rv WHERE rv.task_id = t.task_id)      AS reviews_done
FROM task t
LEFT JOIN campaign c ON c.campaign_id = t.campaign_id;

COMMIT;
