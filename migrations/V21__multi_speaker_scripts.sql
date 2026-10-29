-- ============================================================
-- V21 — NHIỀU SPEAKER DÙNG CHUNG MỘT CẶP CÂU
--
-- 1. task_script có khoá thay thế `task_script_id`: một cặp câu có thể có nhiều
--    dòng cho nhiều Speaker trong cùng task. Duy nhất theo
--    (task_id, script_id, assignee_user_id); dòng chưa gán ai vẫn duy nhất theo
--    (task_id, script_id).
-- 2. recording_id dài hơn để chứa `_u<speaker_id>`, nên hai Speaker thu cùng câu
--    không đụng mã. ID lịch sử (không có `_u`) giữ nguyên và vẫn hợp lệ.
-- 3. Bỏ trigger một-người-đọc; chống trùng còn lại là
--    uq_recording_active_variant (script, variant, speaker).
-- 4. fn_queue_claim_next chỉ phát câu giao cho chính Speaker (hoặc chưa gán ai)
--    và không chặn vì Speaker khác đã có bản.
--
-- Idempotent: chạy lại nhiều lần vẫn an toàn.
-- Cài trên database đang chạy:
--   cat migrations/V21__multi_speaker_scripts.sql \
--     | docker exec -i csl-postgres psql -U csl -d codeswitchlabel -v ON_ERROR_STOP=1
-- ============================================================

BEGIN;

-- ------------------------------------------------------------
-- 1. task_script: khoá thay thế + unique theo từng Speaker.
--    Dữ liệu cũ giữ nguyên task_script_id được cấp tuần tự.
-- ------------------------------------------------------------
ALTER TABLE task_script
    ADD COLUMN IF NOT EXISTS task_script_id BIGINT GENERATED ALWAYS AS IDENTITY;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
          FROM pg_constraint c
          JOIN pg_class t ON t.oid = c.conrelid
         WHERE t.relname = 'task_script'
           AND c.contype = 'p'
           AND pg_get_constraintdef(c.oid) = 'PRIMARY KEY (task_script_id)'
    ) THEN
        ALTER TABLE task_script DROP CONSTRAINT IF EXISTS task_script_pkey;
        ALTER TABLE task_script
            ADD CONSTRAINT task_script_pkey PRIMARY KEY (task_script_id);
    END IF;
END $$;

CREATE UNIQUE INDEX IF NOT EXISTS uq_task_script_assignee
    ON task_script (task_id, script_id, assignee_user_id)
    WHERE assignee_user_id IS NOT NULL;
CREATE UNIQUE INDEX IF NOT EXISTS uq_task_script_unassigned
    ON task_script (task_id, script_id)
    WHERE assignee_user_id IS NULL;

-- ------------------------------------------------------------
-- 2. Nới cột ID bản ghi. View tham chiếu cột phải tháo trước.
-- ------------------------------------------------------------
DROP VIEW IF EXISTS v_speaker_performance;
DROP VIEW IF EXISTS v_dataset_missing_pair;

ALTER TABLE recording         ALTER COLUMN recording_id TYPE VARCHAR(64);
ALTER TABLE task_recording    ALTER COLUMN recording_id TYPE VARCHAR(64);
ALTER TABLE review            ALTER COLUMN recording_id TYPE VARCHAR(64);
ALTER TABLE dataset_recording ALTER COLUMN recording_id TYPE VARCHAR(64);
ALTER TABLE audit_log         ALTER COLUMN entity_id   TYPE VARCHAR(64);

-- ------------------------------------------------------------
-- 3. Định dạng ID: thêm `_u<speaker>` (tuỳ chọn) trước `_tN`.
-- ------------------------------------------------------------
ALTER TABLE recording DROP CONSTRAINT IF EXISTS ck_recording_id_format;
ALTER TABLE recording DROP CONSTRAINT IF EXISTS ck_recording_script_serial;

ALTER TABLE recording ADD CONSTRAINT ck_recording_id_format CHECK
    (recording_id ~ '^r_(cs|vi)_([0-9]{9}|[0-9]{12})(_u[1-9][0-9]*)?(_t([2-9]|[1-9][0-9]+))?$');

ALTER TABLE recording ADD CONSTRAINT ck_recording_script_serial CHECK
    (regexp_replace(recording_id, '^r_(cs|vi)_([0-9]{9}|[0-9]{12})(_u[1-9][0-9]*)?(_t([2-9]|[1-9][0-9]+))?$', '\2')
     = substr(script_id, 3));

-- ------------------------------------------------------------
-- 4. Bỏ luật một cặp câu một người đọc.
-- ------------------------------------------------------------
DROP TRIGGER IF EXISTS trg_recording_single_speaker ON recording;
DROP FUNCTION IF EXISTS fn_recording_single_speaker();

DROP INDEX IF EXISTS uq_recording_active_variant;
CREATE UNIQUE INDEX uq_recording_active_variant
    ON recording (script_id, sentence_variant, speaker_id)
    WHERE status IN ('pending_review', 'approved');

-- ------------------------------------------------------------
-- 5. Hàm sinh ID nhận Speaker. Bỏ bản 3 tham số để không còn overload
--    (bản 3 tham số vẫn sinh ID cũ không có `_u` — giữ cho dữ liệu lịch sử).
-- ------------------------------------------------------------
DROP FUNCTION IF EXISTS fn_generate_recording_id(VARCHAR, sentence_variant, INT);

CREATE OR REPLACE FUNCTION fn_generate_recording_id(
    p_script_id VARCHAR(16),
    p_variant   sentence_variant,
    p_take      INT DEFAULT 1,
    p_speaker_id BIGINT DEFAULT NULL
) RETURNS VARCHAR(64) AS $$ BEGIN
    IF p_script_id !~ '^s_([0-9]{9}|[0-9]{12})$' THEN
        RAISE EXCEPTION 'invalid script_id %', p_script_id;
    END IF;
    IF p_take < 1 THEN
        RAISE EXCEPTION 'take must be >= 1';
    END IF;
    RETURN 'r_'
        || (CASE p_variant WHEN 'code_switching'   THEN 'cs'
                           WHEN 'pure_vietnamese' THEN 'vi' END)
        || '_'
        || substr(p_script_id, 3)
        || (CASE WHEN p_speaker_id IS NOT NULL THEN '_u' || p_speaker_id::text ELSE '' END)
        || (CASE WHEN p_take > 1 THEN '_t' || p_take::text ELSE '' END);
END $$ LANGUAGE plpgsql;

-- ------------------------------------------------------------
-- 6. Hàng đợi ghi âm: chỉ phát câu của chính Speaker, không chặn vì
--    bản của người khác.
-- ------------------------------------------------------------
CREATE OR REPLACE FUNCTION fn_queue_claim_next(
    p_user_id   BIGINT,
    p_stage     queue_stage,
    p_task_id   BIGINT DEFAULT NULL,
    p_ttl_minutes INT DEFAULT NULL
) RETURNS TABLE (
    script_id     VARCHAR,
    queue_id      BIGINT,
    enqueued_at   TIMESTAMPTZ,
    claim_expires_at TIMESTAMPTZ
) AS $$
DECLARE
    v_ttl    INT;
    v_row    script_queue%ROWTYPE;
    v_campaign BIGINT;
BEGIN
    SELECT COALESCE(NULLIF(config_value, '')::int, 30)
      INTO v_ttl
      FROM system_config WHERE config_key = 'queue.claim_ttl_minutes';
    IF v_ttl IS NULL OR v_ttl < 1 THEN
        v_ttl := 30;
    END IF;
    v_ttl := COALESCE(p_ttl_minutes, v_ttl);

    -- Chặng ghi âm làm việc theo CHIẾN DỊCH: câu của mọi task thu âm trong cùng đợt
    -- đều phát chung cho những Speaker đang nhận việc của đợt. Task độc lập (campaign
    -- NULL) vẫn giới hạn trong chính nó.
    IF p_task_id IS NOT NULL THEN
        SELECT t.campaign_id INTO v_campaign FROM task t WHERE t.task_id = p_task_id;
    END IF;

    SELECT q.* INTO v_row
      FROM script_queue q
      JOIN script s ON s.script_id = q.script_id
     WHERE q.stage = p_stage
       AND s.status = CASE q.stage
                         WHEN 'text_review' THEN 'pending_validation'::script_status
                         ELSE 'validated'::script_status
                       END
       -- Chưa ai giữ, hoặc đã giác người và hết hạn.
       AND (q.state = 'queued' OR (q.state = 'claimed' AND q.claim_expires_at <= now()))
       -- Chặng nào cũng phải tôn trọng p_task_id, nhưng cách "thuộc task" khác nhau:
       --   duyệt văn bản -> task gắn thẳng vào hàng (q.task_id), do lô của Task Manager.
       --   ghi âm       -> task gắn qua bảng trung gian task_script, vì một câu có thể
       --                   nằm trong nhiều task cùng lúc.
       -- Bỏ điều kiện này thì p_task_id trở thành vô nghĩa: mọi hàng đều task_id NULL
       -- ở chặng ghi âm nên câu ngoài task cũng lọt vào.
       AND (
            p_task_id IS NULL
            OR CASE q.stage
                 WHEN 'text_review' THEN (
                     q.task_id IS NULL
                     OR q.task_id = p_task_id
                     OR EXISTS (SELECT 1 FROM task_assignment a
                                 WHERE a.task_id = q.task_id
                                   AND a.user_id = p_user_id
                                   AND a.assignment_status IN ('active', 'completed'))
                 )
                 ELSE EXISTS (
                     SELECT 1
                       FROM task_script ts
                       JOIN task t ON t.task_id = ts.task_id
                      WHERE ts.script_id = q.script_id
                        AND ts.status = 'pending'
                        -- Chỉ câu giao cho chính Speaker này (hoặc chưa gán ai). Hai
                        -- Speaker có thể nhận cùng câu, mỗi người thấy phần của mình.
                        AND (ts.assignee_user_id IS NULL OR ts.assignee_user_id = p_user_id)
                        AND t.task_type = 'recording'
                        AND t.status <> 'cancelled'
                        AND (t.task_id = p_task_id
                             OR (v_campaign IS NOT NULL AND t.campaign_id = v_campaign))
                 )
               END
           )
      ORDER BY q.enqueued_at, q.queue_id
     FOR UPDATE OF q SKIP LOCKED
     LIMIT 1;

    IF NOT FOUND THEN
        RETURN;
    END IF;

    UPDATE script_queue
       SET state = 'claimed',
           claimed_by = p_user_id,
           claimed_at = now(),
           claim_expires_at = now() + make_interval(mins => v_ttl)
     WHERE script_queue.queue_id = v_row.queue_id;

    RETURN QUERY
    SELECT v_row.script_id, v_row.queue_id, v_row.enqueued_at,
           now() + make_interval(mins => v_ttl);
END $$ LANGUAGE plpgsql;

-- ------------------------------------------------------------
-- 7. Tạo lại view đã tháo ở bước 2.
-- ------------------------------------------------------------
CREATE VIEW v_speaker_performance AS
SELECT
    u.user_id,
    u.full_name,
    COUNT(r.recording_id)                                        AS recordings,
    COUNT(*) FILTER (WHERE r.status = 'approved')                AS approved,
    COUNT(*) FILTER (WHERE r.status = 'rejected')                AS rejected,
    ROUND(100.0 * COUNT(*) FILTER (WHERE r.status = 'approved')
          / NULLIF(COUNT(*) FILTER (WHERE r.status IN ('approved', 'rejected')), 0), 1)
                                                                 AS approval_rate_pct
FROM app_user u
JOIN recording r ON r.speaker_id = u.user_id
GROUP BY u.user_id, u.full_name;

CREATE VIEW v_dataset_missing_pair AS
SELECT
    dr.dataset_id,
    r.script_id,
    COUNT(*) FILTER (WHERE r.sentence_variant = 'code_switching')   AS has_cs,
    COUNT(*) FILTER (WHERE r.sentence_variant = 'pure_vietnamese') AS has_vi
FROM dataset_recording dr
JOIN recording r ON r.recording_id = dr.recording_id
GROUP BY dr.dataset_id, r.script_id
HAVING COUNT(*) FILTER (WHERE r.sentence_variant = 'code_switching') = 0
    OR COUNT(*) FILTER (WHERE r.sentence_variant = 'pure_vietnamese') = 0;

COMMIT;
