-- ============================================================
-- V12 verification (psql): quota riêng cho từng người nhận task.
--
-- Chạy trong transaction ROLLBACK ở cuối nên không để lại dữ liệu.
-- Run: psql -d codeswitchlabel -v ON_ERROR_STOP=1 -f migrations/V12__verify_task_assignment_quota.sql
-- ============================================================
BEGIN;

DO $$
DECLARE
    v_type TEXT;
BEGIN
    SELECT data_type INTO v_type
      FROM information_schema.columns
     WHERE table_name = 'task_assignment' AND column_name = 'target_qty';

    IF v_type IS DISTINCT FROM 'integer' THEN
        RAISE EXCEPTION 'task_assignment.target_qty phải là integer, đang là %', v_type;
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
         WHERE conname = 'ck_task_assignment_target_qty'
    ) THEN
        RAISE EXCEPTION 'thiếu constraint ck_task_assignment_target_qty';
    END IF;

    RAISE NOTICE 'OK — cột quota và constraint đã có.';
END $$;

DO $$
DECLARE
    v_null INT;
    v_overflow INT;
BEGIN
    SELECT COUNT(*) INTO v_null
      FROM task_assignment
     WHERE assignment_status = 'active' AND target_qty IS NULL;

    IF v_null > 0 THEN
        RAISE EXCEPTION 'còn % active assignment chưa được backfill quota', v_null;
    END IF;

    -- Chỉ kiểm các task có đủ chỉ tiêu cho số người nhận; dữ liệu cũ bất thường
    -- (target < số người) được phép nhận tối thiểu 1 mỗi người.
    SELECT COUNT(*) INTO v_overflow
      FROM (
        SELECT a.task_id
          FROM task_assignment a
          JOIN task t ON t.task_id = a.task_id
         WHERE a.assignment_status = 'active'
         GROUP BY a.task_id, t.target_qty
        HAVING COUNT(*) <= t.target_qty
           AND SUM(a.target_qty) <> t.target_qty
      ) x;

    IF v_overflow > 0 THEN
        RAISE EXCEPTION 'có % task có tổng quota khác target_qty', v_overflow;
    END IF;

    RAISE NOTICE 'OK — active assignment có quota và tổng quota khớp chỉ tiêu.';
END $$;

ROLLBACK;
