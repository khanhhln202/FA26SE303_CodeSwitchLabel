-- ============================================================
-- V3 — Mô tả danh mục lý do bằng tiếng Việt.
-- PostgreSQL 13+. Idempotent, single transaction.
-- Run: psql -d codeswitchlabel -f migrations/V3__reason_descriptions_vi.sql
-- Fresh installs get the same result via docs/codeswitchlabel.sql.
--
-- Lý do: GET /api/script-error-reasons và GET /api/rejection-reasons trả thẳng
-- cột description cho UI hiển thị. Seed ban đầu để tiếng Anh, UI lại tiếng Việt
-- nên lý do hiện ra không khớp ngôn ngữ giao diện.
--
-- Chỉ sửa mô tả, KHÔNG đụng reason_code — code là khoá mà frontend và các
-- lượt duyệt đã lưu đang tham chiếu.
-- ============================================================

BEGIN;

UPDATE script_error_reason SET description = 'Câu không có nghĩa rõ ràng'            WHERE reason_code = 'meaningless';
UPDATE script_error_reason SET description = 'Chuyển ngữ Việt - Anh không tự nhiên'  WHERE reason_code = 'unnatural';
UPDATE script_error_reason SET description = 'Lỗi ngữ pháp'                          WHERE reason_code = 'grammar';
UPDATE script_error_reason SET description = 'Lỗi chính tả'                          WHERE reason_code = 'spelling';
UPDATE script_error_reason SET description = 'Câu tiếng Anh và tiếng Việt không tương đương' WHERE reason_code = 'mismatch';
UPDATE script_error_reason SET description = 'Trùng với câu đã có'                   WHERE reason_code = 'duplicate';
UPDATE script_error_reason SET description = 'Lỗi văn bản khác'                      WHERE reason_code = 'other';

UPDATE rejection_reason SET description = 'Nội dung đọc không khớp với văn bản'                  WHERE reason_code = 'content_mismatch';
UPDATE rejection_reason SET description = 'Văn bản câu bị sai (đưa câu về lại bước duyệt nội dung)' WHERE reason_code = 'script_text_error';
UPDATE rejection_reason SET description = 'Có tạp âm hoặc âm thanh xen kẽ'                      WHERE reason_code = 'background_noise';
UPDATE rejection_reason SET description = 'Âm lượng quá nhỏ'                                     WHERE reason_code = 'low_volume';
UPDATE rejection_reason SET description = 'Âm thanh bị vỡ hoặc méo'                               WHERE reason_code = 'clipping_distortion';
UPDATE rejection_reason SET description = 'Khoảng lặng đầu/cuối quá dài'                           WHERE reason_code = 'excess_silence';
UPDATE rejection_reason SET description = 'Phát âm sai từ'                                       WHERE reason_code = 'mispronunciation';
UPDATE rejection_reason SET description = 'Đọc thiếu, thêm hoặc thay từ'                          WHERE reason_code = 'word_skipped_added';
UPDATE rejection_reason SET description = 'Lỗi khác'                                             WHERE reason_code = 'other';

COMMIT;

-- ============================================================
-- ROLLBACK (chạy tay trong transaction, nếu cần):
--
--   BEGIN;
--   UPDATE script_error_reason SET description = 'Sentence has no clear meaning' WHERE reason_code = 'meaningless';
--   UPDATE script_error_reason SET description = 'Unnatural Vietnamese-English code-switching' WHERE reason_code = 'unnatural';
--   UPDATE script_error_reason SET description = 'Grammatical error' WHERE reason_code = 'grammar';
--   UPDATE script_error_reason SET description = 'Spelling / typo' WHERE reason_code = 'spelling';
--   UPDATE script_error_reason SET description = 'cs / ve sentences are not equivalent' WHERE reason_code = 'mismatch';
--   UPDATE script_error_reason SET description = 'Duplicate of an existing script' WHERE reason_code = 'duplicate';
--   UPDATE script_error_reason SET description = 'Other text issue' WHERE reason_code = 'other';
--   UPDATE rejection_reason SET description = 'Spoken content does not match the script text' WHERE reason_code = 'content_mismatch';
--   UPDATE rejection_reason SET description = 'Script text itself is wrong (cycles script back to text review)' WHERE reason_code = 'script_text_error';
--   UPDATE rejection_reason SET description = 'Background noise or interference' WHERE reason_code = 'background_noise';
--   UPDATE rejection_reason SET description = 'Volume too low' WHERE reason_code = 'low_volume';
--   UPDATE rejection_reason SET description = 'Audio clipping or distortion' WHERE reason_code = 'clipping_distortion';
--   UPDATE rejection_reason SET description = 'Leading/trailing silence too long' WHERE reason_code = 'excess_silence';
--   UPDATE rejection_reason SET description = 'Word mispronounced' WHERE reason_code = 'mispronunciation';
--   UPDATE rejection_reason SET description = 'Words skipped, added, or replaced while reading' WHERE reason_code = 'word_skipped_added';
--   UPDATE rejection_reason SET description = 'Other issue' WHERE reason_code = 'other';
--   COMMIT;
-- ============================================================