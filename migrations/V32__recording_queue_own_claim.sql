-- Câu đang do chính Speaker giữ được trả lại cho họ ở lần lấy kế tiếp.
--
-- Trước đây fn_queue_claim_next chỉ nhặt hàng 'queued' hoặc claim đã hết hạn. Khi
-- toàn bộ đường lấy câu đi qua hàng đợi, Speaker thu xong bản cs sẽ không bao giờ
-- nhận lại đúng câu đó để thu bản vi — claim của chính họ vẫn còn hiệu lực.
-- Điều kiện claimed_by = p_user_id giữ nguyên khóa một câu một Speaker: người khác
-- vẫn không nhận được câu đang bị giữ.

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
    v_wave BIGINT;
BEGIN
    SELECT COALESCE(NULLIF(config_value, '')::int, 30)
      INTO v_ttl
      FROM system_config WHERE config_key = 'queue.claim_ttl_minutes';
    IF v_ttl IS NULL OR v_ttl < 1 THEN
        v_ttl := 30;
    END IF;
    v_ttl := COALESCE(p_ttl_minutes, v_ttl);

    IF p_task_id IS NOT NULL THEN
        SELECT t.wave_id INTO v_wave FROM task t WHERE t.task_id = p_task_id;
    END IF;

    SELECT q.* INTO v_row
      FROM script_queue q
      JOIN script s ON s.script_id = q.script_id
     WHERE q.stage = p_stage
       AND s.status = CASE q.stage
                         WHEN 'text_review' THEN 'pending_validation'::script_status
                         ELSE 'validated'::script_status
                       END
       AND (
            q.state = 'queued'
            OR (q.state = 'claimed'
                AND (q.claim_expires_at <= now()
                     OR (q.stage = 'recording' AND q.claimed_by = p_user_id)))
       )
       -- Người này đã có đủ hai bản chờ duyệt/đạt thì câu không còn việc cho họ;
       -- lọc ngay trong SQL để không đốt hết số lần thử của vòng lặp gọi hàm.
       AND NOT (
            q.stage = 'recording'
            AND EXISTS (SELECT 1 FROM recording r
                         WHERE r.script_id = q.script_id
                           AND r.speaker_id = p_user_id
                           AND r.sentence_variant = 'code_switching'
                           AND r.status IN ('pending_review', 'approved'))
            AND EXISTS (SELECT 1 FROM recording r
                         WHERE r.script_id = q.script_id
                           AND r.speaker_id = p_user_id
                           AND r.sentence_variant = 'pure_vietnamese'
                           AND r.status IN ('pending_review', 'approved'))
       )
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
                        AND (ts.assignee_user_id IS NULL OR ts.assignee_user_id = p_user_id)
                        AND t.task_type = 'recording'
                        AND t.status <> 'cancelled'
                        AND (t.task_id = p_task_id
                             OR (v_wave IS NOT NULL AND t.wave_id = v_wave))
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
