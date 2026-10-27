-- ============================================================
-- V12 — Bản ghi bị từ chối KHÔNG còn đẩy cặp câu về duyệt nội dung
--
-- Luật cũ (TEAM_007): đa số từ chối bản ghi VÀ có lý do thuộc nhóm 'content'
-- thì câu quay về 'pending_validation' và mục trong task thu âm bị đánh
-- 'rejected'. Luật đó được viết khi nhóm 'content' còn mã script_text_error —
-- "bản thân câu viết sai". V10 gộp năm mã còn bốn, nhóm 'content' chỉ còn
-- content_mismatch = "Đọc thiếu / Sai văn bản — phần đọc không khớp với văn
-- bản đã cho". Mã đó tả NGƯỜI ĐỌC đọc sai, không phải câu sai.
--
-- Hệ quả trên máy chủ: cứ ai đọc vấp là câu bị đẩy khỏi vòng ghi âm, và người
-- đọc không thu lại được — tầng ghi âm chặn "chỉ thu âm được câu đã duyệt nội
-- dung". 14 cặp câu đang kẹt như vậy, 11 cặp đã mất cả hai bản thu.
--
-- Luật mới: bản ghi bị từ chối là lỗi của BẢN GHI, thu lại là xong; câu giữ
-- nguyên 'validated'. Câu viết sai thật thì đã có đường riêng và đúng việc
-- hơn: đề xuất báo lỗi / sửa câu (script_proposal, TEAM_015), nơi người gửi
-- nói rõ sai chỗ nào và Reviewer chốt.
--
-- Chỉ thay thân hàm fn_review_majority_status. Trigger trg_review_majority
-- giữ nguyên, không đụng bảng hay cột nào.
--
-- Cài trên database ĐANG CHẠY (không dùng `docker compose down -v`):
--   cat migrations/V12__rejection_keeps_script_validated.sql --     | docker exec -i csl-postgres psql -U csl -d codeswitchlabel -v ON_ERROR_STOP=1
--   cat migrations/V12__verify_rejection_keeps_script.sql --     | docker exec -i csl-postgres psql -U csl -d codeswitchlabel -v ON_ERROR_STOP=1
--
-- Idempotent: chạy lại nhiều lần vẫn cho kết quả như nhau.
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
    -- nên bắt người đọc chờ thêm chỉ làm chậm việc thu lại.
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

    -- V12: KHÔNG đẩy câu về duyệt nội dung nữa. Xem ghi chú ở đầu migration.
    RETURN NEW;
END $$ LANGUAGE plpgsql;

-- ------------------------------------------------------------
-- CỐ Ý KHÔNG gỡ kẹt các cặp câu cũ ở đây.
--
-- Đưa chúng về 'validated' bằng một câu UPDATE là không được: trigger
-- fn_script_validated_by_domain_reviewer đòi một lượt duyệt accepted/edited
-- của Reviewer đúng chủ đề, ghi SAU lần đặt lại gần nhất — mà các câu này
-- không có, vì lượt duyệt của chúng có trước lúc bị đẩy về.
--
-- Viết đại một lượt duyệt để lách là bịa chữ ký của người duyệt vào sổ. Đường
-- đúng là để Reviewer duyệt lại qua giao diện; vòng duyệt lại đó đã chạy được
-- từ SR-16 (24/10). Câu lệnh dưới chỉ ĐẾM để biết còn bao nhiêu câu cần duyệt lại.
-- ------------------------------------------------------------
\echo === so cau can Reviewer duyet lai moi thu am tiep duoc ===
SELECT count(*) AS can_duyet_lai
FROM script s
WHERE s.status = 'pending_validation'
  AND EXISTS (SELECT 1 FROM recording r
               WHERE r.script_id = s.script_id AND r.status = 'rejected');
