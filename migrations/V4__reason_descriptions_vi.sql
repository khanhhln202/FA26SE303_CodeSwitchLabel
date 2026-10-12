-- ============================================================
-- V4 — Viết lại mô tả danh mục lý do bằng tiếng Việt cho tự nhiên.
-- PostgreSQL 13+. Idempotent, single transaction.
-- Run: psql -d codeswitchlabel -f migrations/V4__reason_descriptions_vi_tieng_viet_ro_bo.sql
-- Fresh installs get the same result via docs/codeswitchlabel.sql.
--
-- V3 đã đổi sang tiếng Việt, nhưng nhiều mô tả còn dạng bản dịch cứng ngữ, đọc lộn:
--   - "Nội dung đọc không khớp với văn bản"     → chủ ngữ là "nội dung", nghe như lỗi nội dung
--   - "Có tạp âm hoặc âm thanh xen kẽ"          → "âm thanh xen kẽ" rối
--   - "Câu tiếng Anh và tiếng Việt không tương đương" → câu này không phải "cặp câu" mà là 1 câu
--
-- Mỗi mô tả viết thei một khuôn: "<vấn đề> — <điều chỉnh cần làm>".
-- Giao diện hiện description nguyên văn, nên đọc một mạch từ trên xuống phải đủ ý.
--
-- Chỉ sửa mô tả, KHÔNG đụng reason_code — code là khoá mà frontend và các
-- lượt duyệt đã lưu đang tham chiếu.
-- ============================================================

BEGIN;

UPDATE script_error_reason SET description = 'Câu không có nghĩa rõ ràng — nên bỏ và tìm câu khác'            WHERE reason_code = 'meaningless';
UPDATE script_error_reason SET description = 'Chuyển ngữ Việt - Anh không tự nhiên — cần nói lại cho nghe như người bản xứ' WHERE reason_code = 'unnatural';
UPDATE script_error_reason SET description = 'Lỗi ngữ pháp trong câu tiếng Anh hoặc tiếng Việt'                WHERE reason_code = 'grammar';
UPDATE script_error_reason SET description = 'Lỗi chính tả trong câu tiếng Anh hoặc tiếng Việt'              WHERE reason_code = 'spelling';
UPDATE script_error_reason SET description = 'Hai câu không cùng nghĩa — phần tiếng Anh không dịch được sang tiếng Việt' WHERE reason_code = 'mismatch';
UPDATE script_error_reason SET description = 'Câu này đã có trong dữ liệu — không cần gửi lại'                WHERE reason_code = 'duplicate';
UPDATE script_error_reason SET description = 'Lỗi văn bản khác — ghi rõ ở ô bình luận'                      WHERE reason_code = 'other';

UPDATE rejection_reason SET description = 'Đọc sai nội dung — phần đọc không khớp với văn bản đã cho'            WHERE reason_code = 'content_mismatch';
UPDATE rejection_reason SET description = 'Văn bản câu bị sai — câu này sẽ quay về bước duyệt nội dung'          WHERE reason_code = 'script_text_error';
UPDATE rejection_reason SET description = 'Có tạp âm — nghe thấy tiếng khác lẫn vào trong bản ghi'             WHERE reason_code = 'background_noise';
UPDATE rejection_reason SET description = 'Âm lượng quá nhỏ — người nghe phải vặn to mới nghe rõ'              WHERE reason_code = 'low_volume';
UPDATE rejection_reason SET description = 'Âm thanh bị vỡ — nói quá lớn hoặc thu trong lúc bị va chạm'          WHERE reason_code = 'clipping_distortion';
UPDATE rejection_reason SET description = 'Khoảng lặng đầu hoặc cuối quá dài — thu lại phần này'              WHERE reason_code = 'excess_silence';
UPDATE rejection_reason SET description = 'Phát âm sai — đọc chưa đúng, cần tập lại rồi thu'                 WHERE reason_code = 'mispronunciation';
UPDATE rejection_reason SET description = 'Đọc thiếu, thêm hoặc thay từ — phần đọc lệch với văn bản'          WHERE reason_code = 'word_skipped_added';
UPDATE rejection_reason SET description = 'Lỗi khác — ghi rõ ở ô bình luận'                                   WHERE reason_code = 'other';

COMMIT;

-- ============================================================
-- ROLLBACK (chạy tay trong transaction, nếu cần) — trả về bản V3:
--
--   BEGIN;
--   UPDATE script_error_reason SET description = 'Câu không có nghĩa rõ ràng' WHERE reason_code = 'meaningless';
--   UPDATE script_error_reason SET description = 'Chuyển ngữ Việt - Anh không tự nhiên' WHERE reason_code = 'unnatural';
--   UPDATE script_error_reason SET description = 'Lỗi ngữ pháp' WHERE reason_code = 'grammar';
--   UPDATE script_error_reason SET description = 'Lỗi chính tả' WHERE reason_code = 'spelling';
--   UPDATE script_error_reason SET description = 'Câu tiếng Anh và tiếng Việt không tương đương' WHERE reason_code = 'mismatch';
--   UPDATE script_error_reason SET description = 'Trùng với câu đã có' WHERE reason_code = 'duplicate';
--   UPDATE script_error_reason SET description = 'Lỗi văn bản khác' WHERE reason_code = 'other';
--   UPDATE rejection_reason SET description = 'Nội dung đọc không khớp với văn bản' WHERE reason_code = 'content_mismatch';
--   UPDATE rejection_reason SET description = 'Văn bản câu bị sai (đưa câu về lại bước duyệt nội dung)' WHERE reason_code = 'script_text_error';
--   UPDATE rejection_reason SET description = 'Có tạp âm hoặc âm thanh xen kẽ' WHERE reason_code = 'background_noise';
--   UPDATE rejection_reason SET description = 'Âm lượng quá nhỏ' WHERE reason_code = 'low_volume';
--   UPDATE rejection_reason SET description = 'Âm thanh bị vỡ hoặc méo' WHERE reason_code = 'clipping_distortion';
--   UPDATE rejection_reason SET description = 'Khoảng lặng đầu/cuối quá dài' WHERE reason_code = 'excess_silence';
--   UPDATE rejection_reason SET description = 'Phát âm sai từ' WHERE reason_code = 'mispronunciation';
--   UPDATE rejection_reason SET description = 'Đọc thiếu, thêm hoặc thay từ' WHERE reason_code = 'word_skipped_added';
--   UPDATE rejection_reason SET description = 'Lỗi khác' WHERE reason_code = 'other';
--   COMMIT;
-- ============================================================
