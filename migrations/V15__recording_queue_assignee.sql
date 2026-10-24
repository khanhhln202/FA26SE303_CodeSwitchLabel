-- ============================================================
-- V15 — Hàng đợi ghi âm tôn trọng người nhận từng cặp câu
--
-- khi tạo/sửa task thu âm, mỗi cặp câu có thể được gán cho
-- đúng một Speaker (task_script.assignee_user_id). Hàm claim cũ chỉ kiểm
-- tra câu có nằm trong task hay không, nên vẫn phát cặp của Speaker A
-- cho Speaker B.
--
-- Hàm mới:
--   • có p_task_id: chỉ nhận cặp Pending của task đó mà assignee NULL
--     (mục cũ) hoặc đúng người đang gọi;
--   • không có p_task_id: vẫn chặn cặp đã gán cho người khác.
--
-- Idempotent: CREATE OR REPLACE, chạy lại nhiều lần vẫn cho kết quả như nhau.
-- Cột assignee_user_id do V14 bảo đảm; migration này chỉ thay hàm.
-- ============================================================

BEGIN;

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
BEGIN
    SELECT COALESCE(NULLIF(config_value, '')::int, 30)
      INTO v_ttl
      FROM system_config WHERE config_key = 'queue.claim_ttl_minutes';
    IF v_ttl IS NULL OR v_ttl < 1 THEN
        v_ttl := 30;
    END IF;
    v_ttl := COALESCE(p_ttl_minutes, v_ttl);

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
                                   AND a.assignment_status = 'active')
                 )
                 ELSE EXISTS (SELECT 1 FROM task_script ts
                               WHERE ts.script_id = q.script_id
                                 AND ts.task_id = p_task_id
                                 AND ts.status = 'pending'
                                 -- chỉ phát cặp đã gán cho đúng Speaker;
                                 -- NULL là mục cũ chưa gán nên giữ hành vi cũ.
                                 AND (ts.assignee_user_id IS NULL OR ts.assignee_user_id = p_user_id))
               END
           )
       -- cặp đã gán cho Speaker khác thì không phát cho người này, kể cả
       -- khi gọi không kèm task_id. Mục cũ assignee_user_id NULL vẫn dùng chung.
       AND (
            q.stage <> 'recording'
            OR NOT EXISTS (SELECT 1 FROM task_script ts
                            WHERE ts.script_id = q.script_id
                              AND ts.assignee_user_id IS NOT NULL
                              AND ts.assignee_user_id <> p_user_id)
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

COMMIT;
