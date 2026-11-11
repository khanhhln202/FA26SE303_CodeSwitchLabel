-- V26 — Công tắc cho chế độ máy duyệt TỰ ĐỘNG
--
-- Trước bản này máy chỉ vào khi có người gọi endpoint. Hai Reviewer duyệt lệch nhau xong
-- thì bản ghi nằm ở thế hoà cho tới khi ai đó nhớ ra mà bấm. Bản này thêm công tắc cho
-- một tiến trình nền tự quét hàng đợi đó và phán.
--
-- KHÔNG đổi recording_status. Bản ghi vẫn giữ 'pending_review' cho tới khi máy bỏ phiếu
-- lượt ba, rồi fn_review_majority_status chốt như cũ. Thêm một giá trị vào ENUM sẽ kéo
-- theo rà lại 36 chỗ lọc theo pending_review trong mã nguồn — hàng đợi reviewer, hạn mức
-- giao việc, thống kê, xuất dataset — và sót một chỗ là hỏng âm thầm.

INSERT INTO system_config (config_key, config_value, value_type, description) VALUES
    ('ai_review.auto_enabled', 'false', 'bool',
     'Cho tien trinh nen tu quet cac ban ghi dang hoa 1-1 va goi may duyet. CO Y mac dinh '
     'TAT: bat len la may chu bat dau tieu CPU deu dan cho nhan dang giong noi, nen phai la '
     'quyet dinh co y thuc chu khong phai he qua cua mot lan deploy. Van can '
     'ai_review.enabled = true thi may moi phan duoc.'),

    ('ai_review.auto_interval_seconds', '60', 'string',
     'Nghi bao lau giua hai luot quet, tinh bang giay. Moi ban ghi ton khoang 50 giay nhan '
     'dang tren CPU, nen de qua ngan cung khong nhanh hon duoc — chi lam log day them.'),

    ('ai_review.auto_batch_size', '3', 'string',
     'Toi da bao nhieu ban ghi moi luot quet. Xu ly TUAN TU chu khong song song: moi lan '
     'nhan dang doi khoang 1 GB RAM, chay chong len nhau la may chu het bo nho.')
ON CONFLICT (config_key) DO NOTHING;
