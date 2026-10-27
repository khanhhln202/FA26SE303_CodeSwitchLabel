-- ============================================================
-- V20 — hàng đợi ghi âm theo CHIẾN DỊCH + bản nháp đề xuất của Reviewer
--
-- 1. `proposal_status` thêm 'deleted': Reviewer loại hẳn một câu không dùng được
--    (câu chuyển 'deactivated', lịch sử đề xuất/duyệt giữ nguyên).
-- 2. `script_proposal` thêm bản nháp Reviewer lưu trên máy chủ. Quyết định duyệt
--    đọc bản nháp này thay vì tin nội dung client gửi kèm.
-- 3. `fn_queue_claim_next`: chặng ghi âm phát câu theo cả chiến dịch, không ghim
--    `task_script.assignee_user_id` nữa.
-- 4. `fn_recording_single_speaker`: bản 'rejected' không còn giữ quyền sở hữu cặp
--    câu — sau khi Reviewer loại, Speaker khác được thu lại.
--
-- Idempotent: chạy lại nhiều lần vẫn an toàn.
-- Cài trên database đang chạy:
--   cat migrations/V20__campaign_queue_and_proposal_drafts.sql \
--     | docker exec -i csl-postgres psql -U csl -d codeswitchlabel -v ON_ERROR_STOP=1
-- ============================================================

ALTER TYPE proposal_status ADD VALUE IF NOT EXISTS 'deleted';

ALTER TABLE script_proposal
    ADD COLUMN IF NOT EXISTS draft_cs_content TEXT,
    ADD COLUMN IF NOT EXISTS draft_vi_content TEXT,
    ADD COLUMN IF NOT EXISTS draft_alignment  JSONB,
    ADD COLUMN IF NOT EXISTS draft_saved_by   BIGINT REFERENCES app_user(user_id),
    ADD COLUMN IF NOT EXISTS draft_saved_at   TIMESTAMPTZ;

-- ------------------------------------------------------------
-- Hàng đợi ghi âm: phạm vi chiến dịch, bỏ ghim người nhận, bỏ quyền sở hữu của
-- bản rejected. Người đã bị từ chối chỉ được nhận lại khi mọi Speaker khác trong
-- đợt cũng đã thử — tránh cả cặp câu kẹt vĩnh viễn.
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
       AND (q.state = 'queued' OR (q.state = 'claimed' AND q.claim_expires_at <= now()))
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
                        AND t.task_type = 'recording'
                        AND t.status <> 'cancelled'
                        AND (t.task_id = p_task_id
                             OR (v_campaign IS NOT NULL AND t.campaign_id = v_campaign))
                 )
               END
           )
       -- Cặp đang có bản chờ duyệt/đạt của người KHÁC thì không phát.
       AND (
            q.stage <> 'recording'
            OR NOT EXISTS (SELECT 1 FROM recording r
                            WHERE r.script_id = q.script_id
                              AND r.speaker_id <> p_user_id
                              AND r.status IN ('pending_review','approved'))
           )
       -- Người đã bị từ chối không nhận lại khi còn Speaker khác chưa thử.
       -- Claim không kèm task/chiến dịch (thu tự do) không có danh sách người để so
       -- nên giữ hành vi cũ: người cũ nhận lại.
       AND (
            q.stage <> 'recording'
            OR NOT EXISTS (SELECT 1 FROM recording r
                            WHERE r.script_id = q.script_id
                              AND r.speaker_id = p_user_id
                              AND r.status = 'rejected')
            OR NOT EXISTS (
                SELECT 1
                  FROM task_assignment a
                  JOIN task t ON t.task_id = a.task_id
                  JOIN app_user u ON u.user_id = a.user_id
                 WHERE a.user_id <> p_user_id
                   AND a.assignment_status IN ('active','completed')
                   AND u.status = 'active'
                   AND t.task_type = 'recording'
                   AND t.status <> 'cancelled'
                   AND (
                        (v_campaign IS NOT NULL AND t.campaign_id = v_campaign)
                        OR (v_campaign IS NULL AND p_task_id IS NOT NULL AND t.task_id = p_task_id)
                       )
                   AND NOT EXISTS (SELECT 1 FROM recording x
                                    WHERE x.script_id = q.script_id
                                      AND x.speaker_id = a.user_id
                                      AND x.status = 'rejected')
               )
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
-- Một cặp câu vẫn chỉ một giọng cho MỖI LẦN THU, nhưng bản bị loại không giữ cặp.
-- ------------------------------------------------------------
CREATE OR REPLACE FUNCTION fn_recording_single_speaker() RETURNS trigger AS $$ BEGIN
    PERFORM pg_advisory_xact_lock(hashtext(NEW.script_id));

    IF EXISTS (SELECT 1 FROM recording r
               WHERE r.script_id = NEW.script_id
                 AND r.speaker_id <> NEW.speaker_id
                 AND r.status NOT IN ('qc_failed','rejected')) THEN
        RAISE EXCEPTION 'script % must be recorded by a single speaker', NEW.script_id;
    END IF;
    RETURN NEW;
END $$ LANGUAGE plpgsql;

-- ------------------------------------------------------------
-- Đề xuất 'deleted' cũng phải trả câu về hàng đợi (trước khi câu bị deactivate).
-- ------------------------------------------------------------
CREATE OR REPLACE FUNCTION fn_script_proposal_sync() RETURNS trigger AS $$ BEGIN
    IF TG_OP = 'INSERT' THEN
        UPDATE script_queue
           SET state = 'dropped'
         WHERE script_id = NEW.script_id
           AND stage = 'recording'
           AND state IN ('queued','claimed');

    ELSIF OLD.status = 'pending' AND NEW.status IN ('accepted','rejected','deleted') THEN
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
