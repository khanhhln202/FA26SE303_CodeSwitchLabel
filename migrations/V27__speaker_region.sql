-- ============================================================
-- V27 — Thêm vùng miền (không bắt buộc) vào hồ sơ người đọc
--
-- VÌ SAO LÀ CỘT RIÊNG chứ không suy ra từ province: province là varchar tự do,
-- không ràng buộc, và dữ liệu thật đã lệch nhau — 'TP. Hồ Chí Minh' và
-- 'Thành phố Hồ Chí Minh' đang là hai giá trị khác nhau trong cùng một bảng.
-- Gom nhóm theo nó không đáng tin. Kiểu ENUM ở đây đảm bảo mọi bản ghi rơi vào
-- đúng ba nhóm, nên thống kê theo vùng miền luôn cộng đúng.
--
-- BA MỨC Bắc/Trung/Nam là cách chia chuẩn cho dữ liệu giọng nói tiếng Việt, và là
-- thứ các bộ ASR tiếng Việt thường ghi kèm. Chia nhỏ hơn thì với cỡ kho hiện nay
-- hầu hết nhóm sẽ rỗng.
--
-- Cột nullable: hồ sơ tạo trước migration này không có dữ liệu, giao diện hiển thị
-- "Chưa cập nhật". KHÔNG backfill từ province — đoán vùng miền từ một ô chữ tự do
-- là tự tạo ra dữ liệu sai mà trông như dữ liệu thật.
--
-- Cài trên database ĐANG CHẠY:
--   cat migrations/V27__speaker_region.sql \
--     | docker exec -i csl-postgres psql -U csl -d codeswitchlabel -v ON_ERROR_STOP=1
--
-- Idempotent: chạy lại nhiều lần vẫn cho kết quả như nhau.
-- ============================================================

DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_type WHERE typname = 'speaker_region') THEN
        CREATE TYPE speaker_region AS ENUM ('north','central','south');
    END IF;
END $$;

ALTER TABLE speaker_profile ADD COLUMN IF NOT EXISTS region speaker_region;

COMMENT ON COLUMN speaker_profile.region IS
    'Vung mien giong noi: north/central/south. Doc lap voi province, vi province la '
    'van ban tu do khong gom nhom duoc. NULL = chua cap nhat.';
