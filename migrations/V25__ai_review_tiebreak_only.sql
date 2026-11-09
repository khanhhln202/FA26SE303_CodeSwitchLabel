-- ============================================================
-- V25 — Máy duyệt chỉ là PHIẾU THỨ BA PHÁ THẾ, nên luôn phải quyết
--
-- Luật nhóm chốt với giảng viên hướng dẫn: hai Reviewer người duyệt trước. Cùng
-- kết quả thì trigger trg_review_majority chốt ngay ở lượt 2 và máy không tham
-- gia. Chỉ khi hai người LỆCH nhau (1 đạt / 1 không đạt) thì máy mới vào.
--
-- V24 dựng bảng theo thiết kế cũ, cho máy "bỏ quyền" khi không chắc. Với luật
-- trên thì bỏ quyền là sai: nó để lại bản ghi 1–1 không ai quyết, tức thế vẫn
-- chưa được phá. V24 KHÔNG được sửa lại vì nó đã áp lên database thật và sổ
-- schema_migration đã giữ checksum của nó — sửa là deploy dừng. Nên đổi ở đây.
--
-- Ba việc:
--   1. Ba khoá ngưỡng cũ thành MỘT ranh giới duy nhất.
--   2. Bỏ trạng thái 'abstained' khỏi ràng buộc verdict.
--   3. review_id thành NOT NULL, và bằng chứng chết theo phiếu (ON DELETE CASCADE).
--
-- Migration KHÔNG tự xoá dữ liệu. Nếu bảng đã có dòng bỏ quyền thì nó dừng và
-- nói ra, để người trực quyết định — cùng tinh thần với deploy.sh, vốn không bao
-- giờ tự phục hồi. Trên mọi môi trường hiện tại bảng còn rỗng vì
-- ai_review.enabled chưa từng bật.
--
-- Cài trên database ĐANG CHẠY:
--   cat migrations/V25__ai_review_tiebreak_only.sql \
--     | docker exec -i csl-postgres psql -U csl -d codeswitchlabel -v ON_ERROR_STOP=1
--
-- Idempotent: chạy lại nhiều lần vẫn cho kết quả như nhau.
-- ============================================================

BEGIN;

-- 0. Cửa an toàn ---------------------------------------------------------------
DO $$
DECLARE
    v_blocking INT;
BEGIN
    SELECT count(*) INTO v_blocking
      FROM ai_review
     WHERE verdict = 'abstained' OR review_id IS NULL;

    IF v_blocking > 0 THEN
        RAISE EXCEPTION
            'ai_review dang co % dong bo quyen. Khong tu xoa du lieu: hay quyet dinh '
            'xu ly tung dong roi chay lai migration nay.', v_blocking;
    END IF;
END $$;

-- 1. Một ranh giới duy nhất ----------------------------------------------------
DELETE FROM system_config
 WHERE config_key IN ('ai_review.wer_reject_min',
                      'ai_review.wer_approve_max',
                      'ai_review.allow_approve_vote');

INSERT INTO system_config (config_key, config_value, value_type, description)
VALUES ('ai_review.wer_decision_threshold', '0.40', 'string',
        'RANH GIOI DUY NHAT. WER duoi muc nay thi may bo phieu approved; tu muc nay tro len '
        'thi rejected voi ly do content_mismatch. May la nguoi pha the nen luon phai chon mot '
        'ben, khong co vung bo quyen. De cao vi ASR gap cau tron Viet-Anh da sai san mot phan; '
        'de thap la loai oan ban doc dung.')
ON CONFLICT (config_key) DO NOTHING;

-- 2. Bỏ 'abstained' ------------------------------------------------------------
-- ck_ai_review_vote_has_review hết nghĩa khi review_id là NOT NULL.
ALTER TABLE ai_review DROP CONSTRAINT IF EXISTS ck_ai_review_vote_has_review;

ALTER TABLE ai_review DROP CONSTRAINT IF EXISTS ck_ai_review_verdict;
ALTER TABLE ai_review ADD  CONSTRAINT ck_ai_review_verdict
    CHECK (verdict IN ('approved', 'rejected'));

-- 3. Mỗi dòng bằng chứng phải gắn với một phiếu thật ---------------------------
ALTER TABLE ai_review ALTER COLUMN review_id SET NOT NULL;

ALTER TABLE ai_review DROP CONSTRAINT IF EXISTS ai_review_review_id_fkey;
ALTER TABLE ai_review ADD  CONSTRAINT ai_review_review_id_fkey
    FOREIGN KEY (review_id) REFERENCES review(review_id) ON DELETE CASCADE;

COMMIT;
