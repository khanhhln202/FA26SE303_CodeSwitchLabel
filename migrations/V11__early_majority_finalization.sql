-- ============================================================
-- V11 — Chốt kết quả duyệt NGAY khi đa số đã chắc
--
-- Luật cũ: đủ `review.rounds_required` lượt (mặc định 3) rồi mới so đa số.
-- Hệ quả: bản ghi bị HAI người từ chối vẫn nằm "chờ duyệt" cho tới khi có
-- người thứ ba bấm, dù lượt thứ ba không lật được kết quả. Người đọc không
-- biết phải thu lại, còn Reviewer thì làm một việc đã có đáp án.
--
-- Luật mới: một bên đạt quá bán ((n / 2) + 1) là chốt luôn. Đủ lượt mà hoà
-- (chỉ xảy ra khi số vòng là số chẵn) thì trượt — y như nhánh cũ.
--
-- Chỉ thay thân hàm fn_review_majority_status. Trigger trg_review_majority
-- giữ nguyên, không đụng bảng hay cột nào.
--
-- Cài trên database ĐANG CHẠY (không dùng `docker compose down -v`):
--   cat migrations/V11__early_majority_finalization.sql --     | docker exec -i csl-postgres psql -U csl -d codeswitchlabel -v ON_ERROR_STOP=1
--   cat migrations/V11__verify_early_majority.sql --     | docker exec -i csl-postgres psql -U csl -d codeswitchlabel -v ON_ERROR_STOP=1
--
-- Idempotent: chạy lại nhiều lần vẫn cho kết quả như nhau.
--
-- Bản ghi CŨ đang kẹt (đã quá bán nhưng chưa đủ lượt) không tự chốt: trigger
-- chỉ chạy khi có lượt duyệt mới. Phần dưới cùng chốt lại chúng một lần.
-- ============================================================

CREATE OR REPLACE FUNCTION fn_review_majority_status() RETURNS trigger AS $$ DECLARE
    v_rounds_required INT;
    v_majority        INT;
    v_total           INT;
    v_approved        INT;
    v_rejected        INT;
    v_final           recording_status;
BEGIN
    SELECT COALESCE(NULLIF(config_value, '')::int, 3)
      INTO v_rounds_required
      FROM system_config
     WHERE config_key = 'review.rounds_required';

    IF v_rounds_required IS NULL OR v_rounds_required < 1 THEN
        v_rounds_required := 3;
    END IF;

    v_majority := (v_rounds_required / 2) + 1;

    SELECT COUNT(*), COUNT(*) FILTER (WHERE decision = 'approved')
      INTO v_total, v_approved
      FROM review WHERE recording_id = NEW.recording_id;

    v_rejected := v_total - v_approved;

    -- Chốt NGAY khi một bên đã quá bán: những lượt còn lại không lật được kết quả nữa,
    -- nên bắt người đọc chờ thêm chỉ làm chậm việc thu lại. Trước đây phải đủ
    -- v_rounds_required lượt mới chốt, nên bản bị hai người từ chối vẫn nằm "chờ duyệt".
    IF v_approved >= v_majority THEN
        v_final := 'approved';
    ELSIF v_rejected >= v_majority THEN
        v_final := 'rejected';
    ELSIF v_total >= v_rounds_required THEN
        -- Chỉ xảy ra khi số vòng là số CHẴN và hai bên hoà (2-2 khi cần 4 lượt):
        -- không đủ phiếu đạt thì trượt, đúng như nhánh cũ.
        v_final := 'rejected';
    ELSE
        RETURN NEW;
    END IF;

    UPDATE recording SET status = v_final
     WHERE recording_id = NEW.recording_id AND status = 'pending_review';

    -- Chỉ khi đa số từ chối bản ghi VÀ có lý do thuộc nhóm 'content' mới trả câu về pending_validation
    IF v_final = 'rejected' THEN
        IF EXISTS (
            SELECT 1
              FROM review_rejection_reason rrr
              JOIN rejection_reason rr ON rr.reason_id = rrr.reason_id
              JOIN review rv ON rv.review_id = rrr.review_id
             WHERE rv.recording_id = NEW.recording_id
               AND rr.category = 'content'
        ) THEN
            UPDATE script SET status = 'pending_validation'
             WHERE script_id = (SELECT script_id FROM recording WHERE recording_id = NEW.recording_id)
               AND status = 'validated';

            -- câu quay về bước duyệt nội dung thì không còn là mục công việc
            -- nữa. Trước đây trigger để nguyên task_script ở 'pending' nên task treo
            -- vĩnh viễn, không bao giờ đạt chỉ tiêu.
            UPDATE task_script SET status = 'rejected'
             WHERE script_id = (SELECT script_id FROM recording WHERE recording_id = NEW.recording_id)
               AND status = 'pending';
        END IF;
    END IF;

    RETURN NEW;
END $$ LANGUAGE plpgsql;

-- ------------------------------------------------------------
-- Chốt lại các bản ghi cũ đã quá bán nhưng còn "chờ duyệt".
--
-- CHỈ đổi trạng thái bản ghi. Cố ý KHÔNG đụng tới nhánh trả câu về
-- pending_validation: câu của chúng có thể đã đi tiếp (được sửa, được giao lô
-- khác) từ lúc đó tới giờ, nên tự ý kéo ngược là phá dữ liệu thật. Lượt duyệt
-- tiếp theo trên chính bản ghi đó sẽ chạy đủ nhánh như thường.
-- ------------------------------------------------------------
WITH diem AS (
    SELECT r.recording_id,
           COUNT(*) FILTER (WHERE v.decision = 'approved') AS so_dat,
           COUNT(*) FILTER (WHERE v.decision = 'rejected') AS so_truot,
           (SELECT (COALESCE(NULLIF(config_value, '')::int, 3) / 2) + 1
              FROM system_config WHERE config_key = 'review.rounds_required') AS qua_ban
      FROM recording r JOIN review v ON v.recording_id = r.recording_id
     WHERE r.status = 'pending_review'
     GROUP BY r.recording_id
)
UPDATE recording r
   SET status = CASE WHEN d.so_dat >= d.qua_ban THEN 'approved'::recording_status
                     ELSE 'rejected'::recording_status END
  FROM diem d
 WHERE r.recording_id = d.recording_id
   AND r.status = 'pending_review'
   AND (d.so_dat >= d.qua_ban OR d.so_truot >= d.qua_ban);
