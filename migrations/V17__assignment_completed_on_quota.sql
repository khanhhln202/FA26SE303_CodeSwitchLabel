-- ============================================================
-- V17 — Lượt giao chuyển 'completed' khi người nhận đạt quota riêng
--
--   active     = chưa đạt quota riêng
--   completed  = đã đạt quota riêng, vẫn còn là người nhận task
--   reassigned = đã bị thay ra khỏi task
--   cancelled  = task đã huỷ
--
-- Trước V17, 'completed' có trong enum nhưng không code path nào set. Từ V17,
-- TaskProgressTracker chấm lại từng lượt giao sau mỗi việc thật:
--   done >= task_assignment.target_qty  → completed
--   done <  quota                       → active
-- nên khi task nâng chỉ tiêu hoặc bản ghi bị từ chối làm done tụt, lượt giao
-- tự mở lại. Completed KHÔNG khoá công việc (TEAM_016): người nhận vẫn thu/duyệt
-- thêm được, giống task Completed vẫn nhận việc.
--
-- Vì completed vẫn là lượt giao còn hiệu lực, ba object đọc trạng thái phải coi
-- Active và Completed như nhau:
--   • unique index uq_task_assignment_active_user  (chặn trùng trên cả hai)
--   • view v_task_progress.assignee_count
--   • fn_queue_claim_next cho chặng duyệt văn bản
--
-- Backfill: chấm lại toàn bộ lượt giao đang mở theo đúng công thức backend dùng
-- (quota riêng, hoặc chia phần nguyên + dư cho dữ liệu trước V12).
--
-- Run: psql -d codeswitchlabel -v ON_ERROR_STOP=1 -f migrations/V17__assignment_completed_on_quota.sql
-- Idempotent: chạy lại nhiều lần vẫn cho kết quả như nhau. Cả file nằm trong một
-- transaction nên lần chạy lỗi để lại database nguyên trạng.
-- ============================================================

BEGIN;

-- ------------------------------------------------------------
-- 1. Unique index: Completed cũng là lượt giao còn hiệu lực.
--
-- Không còn Completed nào được set trước V17 nên dữ liệu hiện tại không thể có
-- hai lượt giao trùng; drop rồi create lại là đủ.
-- ------------------------------------------------------------

DROP INDEX IF EXISTS uq_task_assignment_active_user;

CREATE UNIQUE INDEX uq_task_assignment_active_user
    ON task_assignment (task_id, user_id)
    WHERE assignment_status IN ('active', 'completed');

-- ------------------------------------------------------------
-- 2. View: đếm cả người đã đạt quota riêng.
-- NGUYÊN VĂN từ docs/codeswitchlabel.sql (mục v_task_progress).
-- ------------------------------------------------------------

CREATE OR REPLACE VIEW v_task_progress AS
SELECT
    t.task_id,
    t.campaign_id,
    c.campaign_name,
    t.task_type,
    t.domain,
    t.status        AS task_status,
    t.target_qty,
    t.deadline,
    -- TEAM_010: một task có thể giao cho nhiều người. Chỉ tiêu là của cả nhóm nên
    -- không nhân theo số này — chỉ để báo cáo biết đang chia việc cho bao nhiêu người.
    (SELECT COUNT(*) FROM task_assignment a
        WHERE a.task_id = t.task_id AND a.assignment_status IN ('active', 'completed')) AS assignee_count,
    (SELECT COUNT(*) FROM task_script ts WHERE ts.task_id = t.task_id) AS scripts_assigned,
    (SELECT COUNT(*) FROM task_script ts
        WHERE ts.task_id = t.task_id AND ts.status = 'completed')      AS scripts_completed,
    (SELECT COUNT(*) FROM review rv WHERE rv.task_id = t.task_id)      AS reviews_done
FROM task t
LEFT JOIN campaign c ON c.campaign_id = t.campaign_id;

-- ------------------------------------------------------------
-- 3. fn_queue_claim_next: người đã đạt quota vẫn nhận được câu mới trong task.
-- NGUYÊN VĂN từ docs/codeswitchlabel.sql, chỉ đổi điều kiện assignment_status.
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
                                   AND a.assignment_status IN ('active', 'completed'))
                 )
                 ELSE EXISTS (SELECT 1 FROM task_script ts
                               WHERE ts.script_id = q.script_id
                                 AND ts.task_id = p_task_id
                                 AND ts.status = 'pending'
                                 -- TEAM_021: chỉ phát cặp đã gán cho đúng Speaker;
                                 -- NULL là mục cũ chưa gán nên giữ hành vi cũ.
                                 AND (ts.assignee_user_id IS NULL OR ts.assignee_user_id = p_user_id))
               END
           )
     -- TEAM_021: cặp đã gán cho Speaker khác thì không phát cho người này, kể cả
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

-- ------------------------------------------------------------
-- 4. Backfill: chấm lại mọi lượt giao đang mở theo số việc đã xong.
--
-- Công thức quota khớp backend:
--   • target_qty có sẵn (V12+) → dùng nguyên.
--   • target_qty NULL (dữ liệu cũ) → chia phần nguyên + dư theo thứ tự tên,
--     mỗi người tối thiểu 1 — cùng SplitQuotaAt.
--
-- Task đã huỷ bị bỏ qua: CancelAsync đã chuyển các lượt đang chờ sang cancelled;
-- không mở lại lượt nào của task đã dừng.
-- ------------------------------------------------------------

WITH open_assign AS (
    SELECT a.assignment_id,
           a.task_id,
           a.user_id,
           COALESCE(
               a.target_qty,
               GREATEST(
                   1,
                   t.target_qty / COUNT(*) OVER (PARTITION BY a.task_id)
                   + CASE
                         WHEN ROW_NUMBER() OVER (
                                  PARTITION BY a.task_id
                                  ORDER BY u.full_name, a.user_id)
                              <= t.target_qty % COUNT(*) OVER (PARTITION BY a.task_id)
                         THEN 1 ELSE 0
                     END
               )
           ) AS quota
      FROM task_assignment a
      JOIN task t ON t.task_id = a.task_id
      JOIN app_user u ON u.user_id = a.user_id
     WHERE a.assignment_status IN ('active', 'completed')
       AND t.status <> 'cancelled'
),
done_recording AS (
    -- Cặp câu tính cho người đọc đầu tiên của bản không trượt QC — cùng quy tắc
    -- GetAssigneeDoneAsync (trigger single_speaker bảo đảm chỉ một người đọc).
    SELECT ts.task_id,
           sp.speaker_id AS user_id,
           COUNT(*) AS done
      FROM task_script ts
      JOIN LATERAL (
          SELECT r.speaker_id
            FROM recording r
           WHERE r.script_id = ts.script_id
             AND r.status <> 'qc_failed'
           ORDER BY r.recorded_at, r.recording_id
           LIMIT 1
      ) sp ON true
     WHERE ts.status = 'completed'
     GROUP BY ts.task_id, sp.speaker_id
),
done_review AS (
    SELECT rv.task_id,
           rv.reviewer_id AS user_id,
           COUNT(*) AS done
      FROM review rv
     WHERE rv.reviewer_id IS NOT NULL
     GROUP BY rv.task_id, rv.reviewer_id
),
done AS (
    SELECT task_id, user_id, SUM(done) AS done
      FROM (
          SELECT task_id, user_id, done FROM done_recording
          UNION ALL
          SELECT task_id, user_id, done FROM done_review
      ) x
     GROUP BY task_id, user_id
)
UPDATE task_assignment a
   SET assignment_status = CASE
        WHEN COALESCE(d.done, 0) >= oa.quota THEN 'completed'::assignment_status
        ELSE 'active'::assignment_status
       END
  FROM open_assign oa
  LEFT JOIN done d ON d.task_id = oa.task_id AND d.user_id = oa.user_id
 WHERE a.assignment_id = oa.assignment_id;

COMMIT;
