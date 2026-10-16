-- ============================================================
-- V10 — Lý do từ chối bản ghi còn ĐÚNG 4 lý do (TEAM_016)
--
-- Bốn lý do Reviewer chọn khi từ chối bản ghi, mỗi nhóm đúng MỘT mã:
--   content        → content_mismatch    Đọc thiếu / Sai văn bản
--   audio_quality  → background_noise    Tạp âm / Tiếng ồn môi trường
--   pronunciation  → mispronunciation    Phát âm sai (Code-Switching)
--   other          → other               Lỗi khác
--
-- Năm mã cũ cùng nhóm (script_text_error, word_skipped_added, low_volume,
-- clipping_distortion, excess_silence) bị ẩn (is_active = false) — không xoá
-- dòng nào, đúng luật "lý do chỉ ẩn, không xoá". Lịch sử dùng chúng được gộp
-- về mã giữ lại cùng nhóm để thống kê (v_rejection_reason_stats) chỉ còn 4 mã.
--
-- Cài trên database ĐANG CHẠY (không dùng `docker compose down -v`):
--   cat migrations/V10__four_rejection_reasons.sql \
--     | docker exec -i csl-postgres psql -U csl -d codeswitchlabel -v ON_ERROR_STOP=1
--   cat migrations/V10__verify_four_rejection_reasons.sql \
--     | docker exec -i csl-postgres psql -U csl -d codeswitchlabel -v ON_ERROR_STOP=1
--
-- Idempotent: chạy lại nhiều lần vẫn cho kết quả như nhau.
--
-- Không có BEGIN/COMMIT bao ngoài (cùng lý do với V8/V9): chạy lại sau khi đã
-- xong một phần phải tiếp tục được từ chỗ dở.
-- ============================================================

-- ------------------------------------------------------------
-- 1. Mô tả mới cho 4 mã giữ lại — đúng câu chữ bốn lý do của nghiệp vụ.
--    Bật lại is_active phòng trường hợp ai đó đã ẩn nhầm trước đó.
-- ------------------------------------------------------------
UPDATE rejection_reason
   SET description = 'Đọc thiếu / Sai văn bản — phần đọc không khớp với văn bản đã cho (thiếu từ, thêm từ hoặc sai nội dung)',
       is_active   = TRUE
 WHERE reason_code = 'content_mismatch';

UPDATE rejection_reason
   SET description = 'Tạp âm / Tiếng ồn môi trường — nghe thấy tiếng khác lẫn vào bản ghi',
       is_active   = TRUE
 WHERE reason_code = 'background_noise';

UPDATE rejection_reason
   SET description = 'Phát âm sai (Code-Switching) — đọc chưa đúng, cần tập lại rồi thu',
       is_active   = TRUE
 WHERE reason_code = 'mispronunciation';

UPDATE rejection_reason
   SET description = 'Lỗi khác — ghi rõ ở ô bình luận',
       is_active   = TRUE
 WHERE reason_code = 'other';

-- ------------------------------------------------------------
-- 2. Gộp lịch sử: mỗi lượt từ chối từng dùng mã cũ được chuyển về mã giữ
--    lại CÙNG NHÓM (ON CONFLICT để lượt đã có sẵn mã giữ lại không lỗi),
--    rồi xoá các dòng cũ đang trỏ vào mã sắp bị ẩn.
--    Lưu ý: word_skipped_added mang nhóm pronunciation trong lược đồ cũ.
-- ------------------------------------------------------------
INSERT INTO review_rejection_reason (review_id, reason_id)
SELECT rrr.review_id, kept.reason_id
  FROM review_rejection_reason rrr
  JOIN rejection_reason oldr ON oldr.reason_id = rrr.reason_id
  JOIN rejection_reason kept ON kept.reason_code = CASE oldr.reason_code
        WHEN 'script_text_error'   THEN 'content_mismatch'
        WHEN 'word_skipped_added'  THEN 'mispronunciation'
        WHEN 'low_volume'          THEN 'background_noise'
        WHEN 'clipping_distortion' THEN 'background_noise'
        WHEN 'excess_silence'      THEN 'background_noise'
      END
 WHERE oldr.reason_code IN ('script_text_error','word_skipped_added',
                            'low_volume','clipping_distortion','excess_silence')
ON CONFLICT (review_id, reason_id) DO NOTHING;

DELETE FROM review_rejection_reason rrr
 USING rejection_reason rr
 WHERE rrr.reason_id = rr.reason_id
   AND rr.reason_code IN ('script_text_error','word_skipped_added',
                          'low_volume','clipping_distortion','excess_silence');

-- ------------------------------------------------------------
-- 3. Ẩn năm mã cũ — giữ dòng dấu vết, không cho chọn nữa.
--    (API GET /api/rejection-reasons mặc định activeOnly=true nên chúng
--     tự biến mất khỏi giao diện chọn lý do.)
-- ------------------------------------------------------------
UPDATE rejection_reason
   SET is_active = FALSE
 WHERE reason_code IN ('script_text_error','word_skipped_added',
                       'low_volume','clipping_distortion','excess_silence');
