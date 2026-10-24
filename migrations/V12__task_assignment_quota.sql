-- ============================================================
-- V12 — Quota riêng cho từng người nhận task
--
-- Trước đây task.target_qty là chỉ tiêu của CẢ NHÓM, còn Speaker chỉ thấy
-- "mục tiêu" chia đều tạm tính. Màn hình phân công mới cần đặt quota riêng
-- cho từng người và giữ tổng quota đúng bằng target_qty của task.
--
-- Cột mới: task_assignment.target_qty
--   - NULL = lượt giao cũ trước migration; backend chia đều khi đọc.
--   - > 0  = quota của người nhận.
--   Tổng quota không vượt task.target_qty do service bảo đảm (database chỉ
--   chặn số 0/âm, vì trigger cộng dồn sẽ phức tạp khi target thay đổi).
--
-- Backfill: chia đều active assignment của từng task theo kiểu phần nguyên + dư,
-- cùng công thức backend dùng. Task có ít chỉ tiêu hơn số người nhận (dữ liệu
-- cũ bất thường) nhận tối thiểu 1 mỗi người.
--
-- Idempotent: chạy lại nhiều lần vẫn cho kết quả như nhau.
-- ============================================================

BEGIN;

ALTER TABLE task_assignment
    ADD COLUMN IF NOT EXISTS target_qty INT;

ALTER TABLE task_assignment
    DROP CONSTRAINT IF EXISTS ck_task_assignment_target_qty;

ALTER TABLE task_assignment
    ADD CONSTRAINT ck_task_assignment_target_qty
    CHECK (target_qty IS NULL OR target_qty > 0);

WITH active AS (
    SELECT a.assignment_id,
           t.target_qty AS task_target,
           COUNT(*) OVER (PARTITION BY a.task_id)::int AS assignee_count,
           ROW_NUMBER() OVER (
               PARTITION BY a.task_id
               ORDER BY a.assigned_at, a.assignment_id)::int AS position
      FROM task_assignment a
      JOIN task t ON t.task_id = a.task_id
     WHERE a.assignment_status = 'active'
)
UPDATE task_assignment a
   SET target_qty = CASE
        WHEN active.task_target < active.assignee_count THEN 1
        ELSE active.task_target / active.assignee_count
             + CASE
                 WHEN active.position <= active.task_target % active.assignee_count
                 THEN 1 ELSE 0
               END
       END
  FROM active
 WHERE a.assignment_id = active.assignment_id
   AND a.target_qty IS NULL;

COMMIT;
