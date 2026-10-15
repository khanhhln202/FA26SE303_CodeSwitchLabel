-- ============================================================
-- V8 verification (psql): trạng thái còn ba giá trị mỗi loại (TEAM_013).
--
-- Chạy trong transaction mà ROLLBACK ở cuối nên không để lại dữ liệu nào.
-- Run: psql -d codeswitchlabel -v ON_ERROR_STOP=1 -f migrations/V8__verify_status_three_states.sql
-- ============================================================
BEGIN;

-- ------------------------------------------------------------------
-- 1. Kiểu ENUM đúng ba giá trị, đúng thứ tự.
-- ------------------------------------------------------------------
DO $$
DECLARE
    v_task_vals     TEXT[];
    v_campaign_vals TEXT[];
BEGIN
    SELECT array_agg(e.enumlabel ORDER BY e.enumsortorder)
      INTO v_task_vals
      FROM pg_enum e JOIN pg_type t ON t.oid = e.enumtypid
     WHERE t.typname = 'task_status';

    SELECT array_agg(e.enumlabel ORDER BY e.enumsortorder)
      INTO v_campaign_vals
      FROM pg_enum e JOIN pg_type t ON t.oid = e.enumtypid
     WHERE t.typname = 'campaign_status';

    IF v_task_vals IS DISTINCT FROM ARRAY['in_progress','completed','cancelled']::TEXT[] THEN
        RAISE EXCEPTION 'task_status sai: %', v_task_vals;
    END IF;

    IF v_campaign_vals IS DISTINCT FROM ARRAY['open','in_progress','completed']::TEXT[] THEN
        RAISE EXCEPTION 'campaign_status sai: %', v_campaign_vals;
    END IF;

    RAISE NOTICE 'OK  enum đúng ba giá trị mỗi loại';
END $$;

-- ------------------------------------------------------------------
-- 2. Luật trạng thái đợt đúng với dữ liệu đang có.
--
--    Đây là điều Rule 4 cần: migration tính lại status một lần, nhưng nếu
--    câu SQL sai thì hàng nghìn đợt sẽ mang trạng thái sai mà test vẫn xanh.
--    Test ở đây kiểm đúng database đang chạy, không kiểm logic trong SQL.
-- ------------------------------------------------------------------
DO $$
DECLARE
    v_wrong BIGINT;
BEGIN
    SELECT COUNT(*) INTO v_wrong
      FROM campaign c
      LEFT JOIN (
            SELECT campaign_id,
                   COUNT(*) AS total,
                   COUNT(*) FILTER (WHERE status = 'completed') AS done
              FROM task
             WHERE campaign_id IS NOT NULL
             GROUP BY campaign_id
      ) t ON t.campaign_id = c.campaign_id
     WHERE c.status IS DISTINCT FROM (
            CASE
              WHEN COALESCE(t.total, 0) = 0 THEN 'open'::campaign_status
              WHEN COALESCE(t.done, 0) = COALESCE(t.total, 0) THEN 'completed'::campaign_status
              ELSE 'in_progress'::campaign_status
            END);

    IF v_wrong > 0 THEN
        RAISE EXCEPTION '% đợt có trạng thái không khớp số nhiệm vụ thật', v_wrong;
    END IF;

    RAISE NOTICE 'OK  mọi đợt khớp với số nhiệm vụ thật';
END $$;

-- ------------------------------------------------------------------
-- 3. Đợt đã có nhiệm vụ thì không được ở 'open' — đây là lỗi hay gặp nhất
--    khi migration chỉ đổi kiểu ENUM mà quên bước tính lại.
-- ------------------------------------------------------------------
DO $$
DECLARE
    v_wrong BIGINT;
BEGIN
    SELECT COUNT(*) INTO v_wrong
      FROM campaign c
     WHERE c.status = 'open'
       AND EXISTS (SELECT 1 FROM task t WHERE t.campaign_id = c.campaign_id);

    IF v_wrong > 0 THEN
        RAISE EXCEPTION '% đợt có nhiệm vụ nhưng vẫn để trạng thái open', v_wrong;
    END IF;

    RAISE NOTICE 'OK  không đợt nào có nhiệm vụ mà vẫn open';
END $$;

-- ------------------------------------------------------------------
-- 4. Đợt chỉ Completed khi MỌI nhiệm vụ đều Completed.
--    Task đã huỷ không tính là hoàn thành — luật user chốt.
-- ------------------------------------------------------------------
DO $$
DECLARE
    v_wrong BIGINT;
BEGIN
    SELECT COUNT(*) INTO v_wrong
      FROM campaign c
     WHERE c.status = 'completed'
       AND EXISTS (
            SELECT 1 FROM task t
             WHERE t.campaign_id = c.campaign_id
               AND t.status <> 'completed'
       );

    IF v_wrong > 0 THEN
        RAISE EXCEPTION '% đợt ghi Completed nhưng còn nhiệm vụ chưa hoàn thành', v_wrong;
    END IF;

    RAISE NOTICE 'OK  đợt Completed đều không còn nhiệm vụ chưa xong';
END $$;

-- ------------------------------------------------------------------
-- 5. Mặc định của cột khớp với kiểu mới.
-- ------------------------------------------------------------------
DO $$
DECLARE
    v_task_dflt     TEXT;
    v_campaign_dflt TEXT;
BEGIN
    SELECT pg_get_expr(d.adbin, d.adrelid)
      INTO v_task_dflt
      FROM pg_attrdef d JOIN pg_class c ON c.oid = d.adrelid
     WHERE c.relname = 'task' AND d.adnum = (
             SELECT attnum FROM pg_attribute
              WHERE attrelid = c.oid AND attname = 'status');

    SELECT pg_get_expr(d.adbin, d.adrelid)
      INTO v_campaign_dflt
      FROM pg_attrdef d JOIN pg_class c ON c.oid = d.adrelid
     WHERE c.relname = 'campaign' AND d.adnum = (
             SELECT attnum FROM pg_attribute
              WHERE attrelid = c.oid AND attname = 'status');

    IF v_task_dflt IS DISTINCT FROM '''in_progress''::task_status' THEN
        RAISE EXCEPTION 'task.status default sai: %', v_task_dflt;
    END IF;

    IF v_campaign_dflt IS DISTINCT FROM '''open''::campaign_status' THEN
        RAISE EXCEPTION 'campaign.status default sai: %', v_campaign_dflt;
    END IF;

    RAISE NOTICE 'OK  mặc định cột đúng theo kiểu mới';
END $$;

ROLLBACK;