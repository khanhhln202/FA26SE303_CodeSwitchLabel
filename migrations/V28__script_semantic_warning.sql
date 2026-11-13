-- V28 — Cảnh báo lệch nghĩa do AI
--
-- Khi nhãn câu bằng LLM, máy soát hai câu có cùng nghĩa không. Không cùng
-- nghĩa thì lưu một câu cảnh báo tiếng Việt ngắn vào script để Speaker thấy
-- trước khi thu và Reviewer thấy khi duyệt nội dung.
--
-- NULL = không có cảnh báo (câu chưa qua LLM, hoặc LLM thấy khớp nghĩa).
-- Nội dung câu bị sửa thì service xóa cảnh báo cũ; cột này không tự cập nhật.

ALTER TABLE script ADD COLUMN IF NOT EXISTS semantic_warning TEXT;
