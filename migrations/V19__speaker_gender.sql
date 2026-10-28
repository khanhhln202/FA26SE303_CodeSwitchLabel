-- ============================================================
-- V19 — Thêm giới tính (không bắt buộc) vào hồ sơ người đọc
--
-- Cột nullable: hồ sơ tạo trước migration này không có dữ liệu, giao diện
-- hiển thị "Chưa cập nhật". Không backfill, không đặt mặc định.
--
-- Cài trên database ĐANG CHẠY:
--   cat migrations/V19__speaker_gender.sql \
--     | docker exec -i csl-postgres psql -U csl -d codeswitchlabel -v ON_ERROR_STOP=1
--
-- Idempotent: chạy lại nhiều lần vẫn cho kết quả như nhau.
-- ============================================================

DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_type WHERE typname = 'gender') THEN
        CREATE TYPE gender AS ENUM ('male','female','other');
    END IF;
END $$;

ALTER TABLE speaker_profile ADD COLUMN IF NOT EXISTS gender gender;
