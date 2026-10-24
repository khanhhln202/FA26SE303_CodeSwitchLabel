-- ============================================================
-- V17 verification (psql): lượt giao Completed đúng quota, vẫn còn hiệu lực.
--
-- Chạy trong transaction ROLLBACK ở cuối nên không để lại dữ liệu.
-- Run: psql -d codeswitchlabel -v ON_ERROR_STOP=1 -f migrations/V17__verify_assignment_completed_on_quota.sql
-- ============================================================
BEGIN;

-- ------------------------------------------------------------------
-- 1. Enum vẫn đủ bốn giá trị (V17 không bỏ giá trị nào).
-- ------------------------------------------------------------------
DO $$
DECLARE
    v_vals TEXT[];
BEGIN
    SELECT array_agg(e.enumlabel ORDER BY e.enumsortorder)
      INTO v_vals
      FROM pg_enum e JOIN pg_type t ON t.oid = e.enumtypid
     WHERE t.typname = 'assignment_status';

    IF v_vals IS DISTINCT FROM ARRAY['active','completed','reassigned','cancelled']::TEXT[] THEN
        RAISE EXCEPTION 'assignment_status sai: %', v_vals;
    END IF;

    RAISE NOTICE 'OK — assignment_status đủ bốn giá trị.';
END $$;

-- ------------------------------------------------------------------
-- 2. Index / view / function coi Completed là lượt giao còn hiệu lực.
-- ------------------------------------------------------------------
DO $$
DECLARE
    v_index  TEXT;
    v_view   TEXT;
    v_fn     TEXT;
BEGIN
    SELECT indexdef INTO v_index
      FROM pg_indexes
     WHERE schemaname = current_schema()
       AND indexname = 'uq_task_assignment_active_user';

    IF v_index IS NULL OR v_index NOT LIKE '%completed%' THEN
        RAISE EXCEPTION 'unique index chưa tính completed: %', v_index;
    END IF;

    SELECT pg_get_viewdef('v_task_progress'::regclass) INTO v_view;
    IF position('completed' IN v_view) = 0 THEN
        RAISE EXCEPTION 'v_task_progress chưa đếm completed';
    END IF;

    SELECT pg_get_functiondef(p.oid) INTO v_fn
      FROM pg_proc p
      JOIN pg_namespace n ON n.oid = p.pronamespace
     WHERE n.nspname = current_schema()
       AND p.proname = 'fn_queue_claim_next';

    IF v_fn IS NULL OR position('completed' IN v_fn) = 0 THEN
        RAISE EXCEPTION 'fn_queue_claim_next chưa nhận completed';
    END IF;

    RAISE NOTICE 'OK — index, view và function đã coi completed là còn hiệu lực.';
END $$;

-- ------------------------------------------------------------------
-- 3. Backfill khớp số liệu thật: không còn Active nào đã đủ quota và
--    không còn Completed nào chưa đủ quota.
-- ------------------------------------------------------------------
DO $$
DECLARE
    v_wrong BIGINT;
BEGIN
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
    SELECT COUNT(*) INTO v_wrong
      FROM open_assign oa
      JOIN task_assignment a ON a.assignment_id = oa.assignment_id
      LEFT JOIN done d ON d.task_id = oa.task_id AND d.user_id = oa.user_id
     WHERE (COALESCE(d.done, 0) >= oa.quota AND a.assignment_status <> 'completed')
        OR (COALESCE(d.done, 0) <  oa.quota AND a.assignment_status <> 'active');

    IF v_wrong > 0 THEN
        RAISE EXCEPTION 'còn % lượt giao lệch trạng thái so với quota', v_wrong;
    END IF;

    RAISE NOTICE 'OK — mọi lượt giao đang mở khớp trạng thái với quota.';
END $$;

ROLLBACK;
