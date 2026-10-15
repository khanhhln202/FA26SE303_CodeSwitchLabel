-- ============================================================
-- V8 — Trạng thái còn ba giá trị mỗi loại (TEAM_013)
--
--   task_status:     draft, open, in_progress, completed, cancelled
--                →  in_progress, completed, cancelled
--   campaign_status: draft, open, in_progress, completed, cancelled
--                →  open, in_progress, completed
--
-- Trạng thái đợt giờ là HÀM của các task bên trong, không ai bấm tay:
-- chưa có nhiệm vụ nào → open, có nhiệm vụ → in_progress,
-- mọi nhiệm vụ đều completed → completed.
--
-- Run: psql -d codeswitchlabel -v ON_ERROR_STOP=1 -f migrations/V8__status_three_states.sql
-- Idempotent: chạy lại nhiều lần vẫn cho kết quả như nhau.
--
-- Không có BEGIN/COMMIT bao ngoài: chạy lại sau khi đã xong một phần phải
-- tiếp tục được từ chỗ dở, mà transaction bọc ngoài sẽ rollback cả phần đã
-- đúng. Deploy chạy psql với ON_ERROR_STOP=1 nên câu nào lỗi thì dừng ngay —
-- sửa rồi chạy lại, bước 0 dọn phần còn lại của lần trước.
-- ============================================================

-- ------------------------------------------------------------
-- 0. Dọn phần còn lại của lần chạy trước bị ngắt giữa chừng.
--
-- Lần chạy thất bại có thể để lại kiểu tạm `*_v8_new`. Lệnh dưới đây dọn sạch
-- để chạy lại an toàn. Không DROP gì khác: kiểu chính `task_status` /
-- `campaign_status` chỉ bị thay khi bước 2 chạy tới cùng.
-- ------------------------------------------------------------

DROP TYPE IF EXISTS task_status_v8_new;
DROP TYPE IF EXISTS campaign_status_v8_new;

-- ------------------------------------------------------------
-- 1. Đưa dữ liệu cũ về giá trị còn tồn tại TRƯỚC khi đổi kiểu.
--
-- Phải làm bước này trước: khi đổi kiểu bằng USING status::text::kiểu_mới,
-- hàng nào còn giữ giá trị cũ sẽ làm cả lệnh ALTER TABLE thất bại.
--
-- So sánh qua text thay vì literal ENUM trực tiếp: sau khi kiểu đã đổi,
-- literal 'draft' không còn là giá trị hợp lệ của kiểu và WHERE sẽ lỗi
-- "invalid input value for enum", làm chạy lại thất bại. Ép qua text thì
-- chạy lại chỉ ra UPDATE 0.
-- ------------------------------------------------------------

-- task: 'draft' (chưa giao) và 'open' (đã giao mà chưa làm) gộp vào 'in_progress'.
UPDATE task SET status = 'in_progress'
 WHERE status::text IN ('draft', 'open');

-- campaign: 'draft' và 'cancelled' gộp vào 'open'. Bước 3 tính lại từ task,
-- nên ở đây chỉ cần giá trị tồn tại hợp lệ.
UPDATE campaign SET status = 'open'
 WHERE status::text IN ('draft', 'cancelled');


-- ------------------------------------------------------------
-- 2. Đổi kiểu ENUM bằng recreate-and-swap.
--
-- PostgreSQL KHÔNG có `ALTER TYPE ... DROP VALUE`: giá trị ENUM chỉ thêm được
-- chứ không xóa được. Cách duy nhất là tạo kiểu mới → đổi cột sang kiểu mới
-- bằng USING (ép qua text để giữ nguyên tên giá trị) → xóa kiểu cũ → đổi tên.
-- Lần chạy đầu, sau bước 1 không còn hàng nào giữ giá trị cũ nên USING không
-- thể thất bại vì dữ liệu — chỉ có thể vì kiểu mới khai báo sai, lúc đó
-- ON_ERROR_STOP dừng ngay.
--
-- Các view đọc cột status phải gỡ trước khi đổi kiểu — nếu không ALTER ... TYPE
-- thất bại vì "column used by a view or rule". Định nghĩa dựng lại ở bước 2b
-- là NGUYÊN VĂN từ docs/codeswitchlabel.sql (dòng 1550, 1602, 1624),
-- KHÔNG tự viết lại — lệch một chữ là production và database mới khác nhau.
-- ------------------------------------------------------------

DROP VIEW IF EXISTS v_dashboard_summary;
DROP VIEW IF EXISTS v_campaign_progress;
DROP VIEW IF EXISTS v_task_progress;

-- Kiểu tạm chỉ tạo khi cột còn mang kiểu cũ: chạy lại sau khi đã xong thì cột
-- đã là kiểu mới nên bỏ qua toàn bộ.
--
-- Dấu hiệu "đã xong" là kiểu `task_status` hiện tại chỉ còn đúng ba giá trị
-- (đếm qua pg_enum), chứ KHÔNG phải tên kiểu — vì trước và sau migration tên
-- kiểu đều là `task_status`, chỉ khác số giá trị.
DO $$
BEGIN
    IF (SELECT COUNT(*) FROM pg_enum e JOIN pg_type t ON t.oid = e.enumtypid
         WHERE t.typname = 'task_status') <> 3 THEN
        CREATE TYPE task_status_v8_new     AS ENUM ('in_progress', 'completed', 'cancelled');
        CREATE TYPE campaign_status_v8_new AS ENUM ('open', 'in_progress', 'completed');
    ELSE
        RAISE NOTICE 'task/campaign đã mang kiểu ENUM mới, bỏ qua bước đổi kiểu.';
    END IF;
END $$;

-- Mặc định cột đang giữ kiểu cũ nên phải gỡ trước — nếu không ALTER ... TYPE
-- thất bại vì "default cannot be cast automatically". Bước 4 đặt lại sau.
-- Chạy lại thì DROP DEFAULT chỉ gỡ mặc định đã đặt ở bước 4 của lần trước —
-- an toàn vì bước 4 sẽ đặt lại ngay sau đó.
ALTER TABLE task     ALTER COLUMN status DROP DEFAULT;
ALTER TABLE campaign ALTER COLUMN status DROP DEFAULT;

DO $$
BEGIN
    IF (SELECT COUNT(*) FROM pg_enum e JOIN pg_type t ON t.oid = e.enumtypid
         WHERE t.typname = 'task_status') <> 3 THEN
        ALTER TABLE task     ALTER COLUMN status TYPE task_status_v8_new     USING status::text::task_status_v8_new;
        ALTER TABLE campaign ALTER COLUMN status TYPE campaign_status_v8_new USING status::text::campaign_status_v8_new;

        DROP TYPE task_status;
        DROP TYPE campaign_status;

        ALTER TYPE task_status_v8_new     RENAME TO task_status;
        ALTER TYPE campaign_status_v8_new RENAME TO campaign_status;
    END IF;
END $$;


-- ------------------------------------------------------------
-- 2b. Dựng lại các view đã gỡ ở đầu bước 2 — NGUYÊN VĂN từ
-- docs/codeswitchlabel.sql (dòng 1550, 1602, 1624). Không tự diễn đạt lại.
-- CREATE OR REPLACE để chạy lại an toàn.
-- ------------------------------------------------------------

CREATE OR REPLACE VIEW v_dashboard_summary AS
SELECT
    (SELECT COUNT(*) FROM campaign)                                  AS total_campaigns,
    (SELECT COUNT(*) FROM campaign WHERE status = 'open')             AS open_campaigns,
    (SELECT COUNT(*) FROM campaign WHERE status = 'in_progress')     AS active_campaigns,
    (SELECT COUNT(*) FROM script)                                    AS total_scripts,
    (SELECT COUNT(*) FROM script WHERE status = 'validated')         AS validated_scripts,
    (SELECT COUNT(*) FROM script WHERE status = 'pending_validation') AS pending_scripts,
    (SELECT COUNT(*) FROM recording)                                 AS total_recordings,
    (SELECT COUNT(*) FROM recording WHERE status = 'approved')       AS approved_recordings,
    (SELECT COUNT(*) FROM recording WHERE status = 'rejected')       AS rejected_recordings,
    (SELECT COALESCE(SUM(duration_sec), 0) FROM recording
        WHERE status = 'approved')                                   AS approved_duration_sec,
    (SELECT COUNT(*) FROM speaker_profile)                           AS speakers,
    (SELECT COUNT(*) FROM app_user
        WHERE role_id = (SELECT role_id FROM role WHERE role_name = 'reviewer')) AS reviewers,
    (SELECT COUNT(*) FROM dataset WHERE status = 'released')         AS released_datasets;

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
    (SELECT COUNT(*) FROM task_assignment a
        WHERE a.task_id = t.task_id AND a.assignment_status = 'active') AS assignee_count,
    (SELECT COUNT(*) FROM task_script ts WHERE ts.task_id = t.task_id) AS scripts_assigned,
    (SELECT COUNT(*) FROM task_script ts
        WHERE ts.task_id = t.task_id AND ts.status = 'completed')      AS scripts_completed,
    (SELECT COUNT(*) FROM review rv WHERE rv.task_id = t.task_id)      AS reviews_done
FROM task t
LEFT JOIN campaign c ON c.campaign_id = t.campaign_id;

CREATE OR REPLACE VIEW v_campaign_progress AS
SELECT
    c.campaign_id,
    c.campaign_name,
    c.domain,
    c.target_qty AS campaign_target_qty,
    c.start_date,
    c.end_date,
    c.status AS campaign_status,
    c.assigned_to,
    COALESCE(SUM(t.target_qty), 0) AS allocated_task_qty,
    c.target_qty - COALESCE(SUM(t.target_qty), 0) AS remaining_task_qty,
    COUNT(t.task_id) AS task_count,
    COUNT(t.task_id) FILTER (WHERE t.status = 'completed') AS completed_task_count
FROM campaign c
LEFT JOIN task t ON t.campaign_id = c.campaign_id
GROUP BY
    c.campaign_id,
    c.campaign_name,
    c.domain,
    c.target_qty,
    c.start_date,
    c.end_date,
    c.status,
    c.assigned_to;


-- ------------------------------------------------------------
-- 3. Tính lại trạng thái đợt từ nhiệm vụ thật.
--
-- Đây là điều migration phải tự làm thay cho code: nếu chỉ đổi kiểu ENUM
-- thì mọi đợt sẽ mang giá trị 'open' cho tới lần chạy API đầu tiên —
-- và API chỉ gọi RefreshStatusAsync khi có task đổi, nên đợt đã xong từ trước
-- sẽ hiện sai vĩnh viễn.
--
-- Task 'cancelled' KHÔNG tính là hoàn thành: đợt còn task đã huỷ thì vẫn
-- là 'in_progress'. Đó là luật user chốt.
-- ------------------------------------------------------------

UPDATE campaign c
   SET status = s.next_status
  FROM (
    SELECT c2.campaign_id AS campaign_id,
           CASE
             WHEN COUNT(t.task_id) = 0 THEN 'open'::campaign_status
             WHEN COUNT(t.task_id) FILTER (WHERE t.status = 'completed') = COUNT(t.task_id)
                  THEN 'completed'::campaign_status
             ELSE 'in_progress'::campaign_status
           END AS next_status
      FROM campaign c2
      LEFT JOIN task t ON t.campaign_id = c2.campaign_id
     GROUP BY c2.campaign_id
  ) s
 WHERE s.campaign_id = c.campaign_id
   AND c.status IS DISTINCT FROM s.next_status;

-- ------------------------------------------------------------
-- 4. Mặc định của cột.
-- docs/codeswitchlabel.sql đã có sẵn giá trị mới; câu lệnh này sửa cho
-- database đang chạy nhiều hơn một lần release.
-- ------------------------------------------------------------

ALTER TABLE campaign ALTER COLUMN status SET DEFAULT 'open';
ALTER TABLE task    ALTER COLUMN status SET DEFAULT 'in_progress';
