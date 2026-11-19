-- ============================================================
-- V31 — Luật của máy duyệt: sai một từ là từ chối
--
-- Đổi ai_review.wer_decision_threshold từ 0.40 xuống 0.0001, theo yêu cầu đội frontend
-- ngày 18/11/2026: "nếu chỉ sai một từ so với text thì sẽ từ chối luôn".
--
-- VÌ SAO 0.0001 CHỨ KHÔNG PHẢI 0 — đây là chỗ dễ sai nhất.
-- AiReviewEvaluator so bằng `effectiveWer >= ngưỡng`. Đặt ngưỡng đúng bằng 0 thì
-- `0 >= 0` là ĐÚNG, tức bản ghi đọc KHỚP HOÀN HẢO cũng bị từ chối — từ chối tất cả,
-- không phải "sai một từ là từ chối". 0.0001 nằm lọt giữa "không sai từ nào" (WER = 0)
-- và "sai ít nhất một từ" (WER = 1/số_từ, thấp nhất khoảng 0.001 với kịch bản 1000 từ).
--
-- CÁI GIÁ ĐÃ ĐƯỢC ĐO, ghi lại để người sau khỏi đo lại.
-- Trên 17 lượt máy đã chấm trước khi đổi:
--   -- 8 bản đạt,     WER 0.00 .. 0.30
--   -- 9 bản không đạt, WER 0.63 .. 1.13
--   -- khoảng 0.30 .. 0.63 KHÔNG có bản ghi nào
-- Tức ngưỡng cũ 0.40 nằm giữa một khoảng trống sạch, không phải con số chọn bừa.
-- Với ngưỡng mới, 15 trong 17 bản đó bị từ chối. Trong số bị từ chối oan có
-- r_cs_121001014: kịch bản "em nộp assignment trước 12 giờ 19 hôm nay nhé", máy nghe
-- "Em nộp assignment trước 12h19 hôm nay nhé" — người đọc đúng từng chữ, máy nghe đúng
-- từng chữ, và riêng việc viết "12 giờ 19" thành "12h19" đẩy WER lên 0.30.
--
-- Gốc vấn đề nằm ở cái THƯỚC chứ không ở cái NGƯỠNG: WER đang gộp "người đọc sai",
-- "máy nghe sai" và "chỉ khác cách viết" vào cùng một con số. Chuẩn hoá cách viết số và
-- dấu thanh trước khi so sẽ tách được ba thứ đó. Chưa làm — cần đo cả hai chiều trước.
--
-- KHÔNG đụng vào ai_review.auto_enabled ở đây. Bật tự động trên một bản triển khai dùng
-- ASR tự host là bắt máy chủ tiêu CPU đều đặn, đúng điều V26 cảnh báo. Máy chủ thật đã
-- bật tay ngày 17/11/2026, sau khi chuyển sang dịch vụ nhận dạng bên ngoài.
--
-- Cài trên database ĐANG CHẠY:
--   cat migrations/V31__ai_review_zero_tolerance.sql \
--     | docker exec -i csl-postgres psql -U csl -d codeswitchlabel -v ON_ERROR_STOP=1
-- ============================================================

UPDATE system_config
SET config_value = '0.0001',
    description  = 'Any word error rejects: 0 errors -> WER 0 -> approved, 1+ errors -> '
                   'WER > 0 -> rejected. NOT 0 — the comparison is >=, so 0 would reject '
                   'a perfect match too.',
    updated_at   = now()
WHERE config_key = 'ai_review.wer_decision_threshold';
