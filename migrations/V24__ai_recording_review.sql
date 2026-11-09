-- ============================================================
-- V24 — AI duyệt bản ghi: tài khoản người duyệt máy, bảng bằng chứng, ngưỡng phán
--
-- AI bỏ phiếu như một Reviewer thật, nên nó phải có một dòng app_user: cột
-- review.reviewer_id là khoá ngoại tới app_user(user_id), không nhận giá trị ảo.
--
-- password_hash để một chuỗi KHÔNG phải bcrypt hợp lệ, nên không mật khẩu nào
-- đăng nhập được vào tài khoản này. An toàn vì BcryptPasswordHasher.Verify bắt
-- lỗi parse và trả false thay vì ném 500 — xem Common/PasswordHashing.cs.
--
-- Bảng ai_review giữ BẰNG CHỨNG của mỗi lượt phán: transcript ASR, văn bản gốc,
-- WER. Không nhét vào review.comment vì comment là chỗ người viết cho người đọc,
-- còn đây là dữ liệu máy sinh ra và cần truy vấn được.
--
-- Ràng buộc ck_ai_review_vote_has_review là bất biến thật: verdict có bỏ phiếu
-- thì BẮT BUỘC trỏ tới một dòng review; bỏ quyền thì BẮT BUỘC không trỏ. Đặt ở
-- database để không thể có "AI nói đã từ chối" mà chẳng có lượt duyệt nào.
--
-- Cài trên database ĐANG CHẠY:
--   cat migrations/V24__ai_recording_review.sql \
--     | docker exec -i csl-postgres psql -U csl -d codeswitchlabel -v ON_ERROR_STOP=1
--
-- Idempotent: chạy lại nhiều lần vẫn cho kết quả như nhau.
-- ============================================================

BEGIN;

-- 1. Tài khoản người duyệt máy -------------------------------------------------
-- role_id tra theo TÊN chứ không ghi số: thứ tự trong bảng role không phải hợp đồng.
INSERT INTO app_user (role_id, full_name, email, password_hash, status)
SELECT r.role_id, 'AI Reviewer', 'ai-reviewer@codeswitchlabel.local',
       '!ai-reviewer-no-login', 'active'
  FROM role r
 WHERE r.role_name = 'reviewer'
ON CONFLICT (email) DO NOTHING;

-- 2. Bảng bằng chứng -----------------------------------------------------------
CREATE TABLE IF NOT EXISTS ai_review (
    ai_review_id  bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    recording_id  varchar(64)  NOT NULL REFERENCES recording(recording_id),

    -- null khi AI bỏ quyền: có bằng chứng nhưng không có lượt duyệt nào.
    review_id     bigint       REFERENCES review(review_id) ON DELETE SET NULL,

    model         varchar(120) NOT NULL,
    transcript    text         NOT NULL,
    expected_text text         NOT NULL,

    -- WER giữ dấu tiếng Việt, và WER sau khi bỏ dấu. Phán theo cái nhỏ hơn,
    -- lưu cả hai để tra lại được vì sao máy quyết như vậy.
    --
    -- numeric(9,4) chứ không (6,4): WER KHÔNG bị cắt về 1. ASR trả về chuỗi rác dài
    -- thì tỉ lệ vọt lên rất cao, và đó là tín hiệu thật cần lộ ra chứ không phải lỗi
    -- cần chặn — nhưng (6,4) chỉ chứa tới 99.9999 nên sẽ làm vỡ câu INSERT.
    wer           numeric(9,4) NOT NULL,
    wer_folded    numeric(9,4) NOT NULL,

    verdict       varchar(12)  NOT NULL,
    analyzed_at   timestamptz  NOT NULL DEFAULT now(),
    details       jsonb,

    CONSTRAINT ck_ai_review_verdict
        CHECK (verdict IN ('approved', 'rejected', 'abstained')),

    CONSTRAINT ck_ai_review_wer_range
        CHECK (wer >= 0 AND wer_folded >= 0),

    CONSTRAINT ck_ai_review_vote_has_review
        CHECK ((verdict =  'abstained' AND review_id IS     NULL)
            OR (verdict <> 'abstained' AND review_id IS NOT NULL))
);

CREATE INDEX IF NOT EXISTS idx_ai_review_recording
    ON ai_review (recording_id, analyzed_at DESC);

-- 3. Ngưỡng phán ---------------------------------------------------------------
-- Đặt trong system_config, không nhúng trong code: cùng quy ước với recording.*
-- và quality.* đã có, nên Admin đổi được qua màn hình System Configuration.
INSERT INTO system_config (config_key, config_value, value_type, description) VALUES
    ('ai_review.enabled', 'false', 'bool',
     'Bật tính năng AI duyệt bản ghi. Tắt thì endpoint trả ai_review_disabled.'),

    ('ai_review.wer_reject_min', '0.40', 'string',
     'WER từ mức này trở lên thì AI bỏ phiếu rejected với lý do content_mismatch. '
     'Để cao vì ASR gặp câu trộn Việt-Anh đã sai sẵn một phần; để thấp là loại oan bản tốt.'),

    ('ai_review.wer_approve_max', '0.10', 'string',
     'WER tới mức này thì bản ghi đủ điều kiện được AI duyệt đạt, '
     'nhưng còn phải bật ai_review.allow_approve_vote.'),

    ('ai_review.allow_approve_vote', 'false', 'bool',
     'Mặc định tắt. AI chỉ đo được nội dung, không phán được tạp âm và phát âm — '
     'cho nó bỏ phiếu đạt là âm thầm duyệt đúng hai lỗi nó không thấy. '
     'Bỏ phiếu không đạt sai thì chỉ phải thu lại; bỏ phiếu đạt sai thì dữ liệu rác vào dataset.')
ON CONFLICT (config_key) DO NOTHING;

COMMIT;
