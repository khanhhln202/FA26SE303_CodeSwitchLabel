-- ============================================================
-- V39 — Nội dung bản cam kết, lưu theo phiên bản
--
-- VẤN ĐỀ: user_consent chỉ ghi `version` ('v1') và ngày đồng ý. Nội dung người ta
-- đồng ý thì nằm trong mã nguồn frontend. Hệ quả: sửa một chữ bên frontend là mọi
-- người đã ký 'v1' bỗng dưng "đã đồng ý" một văn bản khác, mà không có dấu vết nào.
-- Với một bản cam kết về DỮ LIỆU SINH TRẮC GIỌNG NÓI thì đó là chỗ không chấp nhận
-- được: phải đọc lại được chính xác người thứ N đã đồng ý chữ gì, vào lúc nào.
--
-- Bảng này giữ nội dung theo phiên bản. Phiên bản đã có người đồng ý thì KHÔNG sửa
-- và KHÔNG xoá — muốn đổi chữ thì phát hành phiên bản mới, đúng như migration.
--
-- Khoá ngoại từ user_consent.version đặt ở CUỐI file, sau khi đã bù đủ dòng cho mọi
-- phiên bản đang tồn tại. Thêm khoá trước khi bù là migration gãy trên database thật.
--
-- Cài trên database ĐANG CHẠY:
--   cat migrations/V39__consent_document.sql \
--     | docker exec -i csl-postgres psql -U csl -d codeswitchlabel -v ON_ERROR_STOP=1
-- ============================================================

BEGIN;

CREATE TABLE IF NOT EXISTS consent_document (
    version        VARCHAR(32) PRIMARY KEY,
    title          VARCHAR(255) NOT NULL,
    body           TEXT         NOT NULL,
    effective_from DATE         NOT NULL,

    -- Đúng MỘT phiên bản đang hiệu lực tại một thời điểm; bản cũ giữ nguyên để tra lại.
    is_active      BOOLEAN      NOT NULL DEFAULT FALSE,

    created_by     BIGINT       REFERENCES app_user(user_id) ON DELETE SET NULL,
    created_at     TIMESTAMPTZ  NOT NULL DEFAULT now(),

    CONSTRAINT ck_consent_document_body_not_blank CHECK (btrim(body) <> '')
);

-- Chỉ một bản hiệu lực. Chỉ mục một phần, nên các bản is_active = false không vướng nhau.
CREATE UNIQUE INDEX IF NOT EXISTS uq_consent_document_active
    ON consent_document ((TRUE)) WHERE is_active;

-- ------------------------------------------------------------
-- Bản nháp v1.
--
-- ĐÂY LÀ BẢN NHÁP, CHƯA CÓ AI DUYỆT. Dòng đầu của nội dung nói rõ điều đó để không ai
-- nhầm là văn bản chính thức. Người chịu trách nhiệm thay chữ thật vào rồi mới phát
-- hành — thay bằng UPDATE nếu CHƯA có ai đồng ý v1, còn đã có người đồng ý thì phát
-- hành v2 chứ không sửa v1.
-- ------------------------------------------------------------
INSERT INTO consent_document (version, title, body, effective_from, is_active)
VALUES (
    'v1',
    '[BẢN NHÁP] Cam kết tham gia thu thập giọng nói — CodeSwitchLabel',
    $consent$[BẢN NHÁP — CHƯA ĐƯỢC DUYỆT. Thay bằng nội dung chính thức trước khi dùng thật.]

1. Việc tôi tham gia
Tôi tự nguyện đọc và thu âm các câu tiếng Việt và câu trộn Việt — Anh do hệ thống
CodeSwitchLabel cung cấp.

2. Dữ liệu được thu thập
- Bản ghi âm giọng nói của tôi.
- Thông tin hồ sơ người đọc: nhóm tuổi, giới tính, vùng miền giọng nói, nơi sinh.
Hệ thống KHÔNG thu thập nội dung nào khác ngoài phần tôi chủ động cung cấp.

3. Mục đích sử dụng
Dữ liệu dùng để xây dựng tập dữ liệu huấn luyện và đánh giá hệ nhận dạng tiếng nói
tiếng Việt có xen tiếng Anh, phục vụ nghiên cứu và học thuật.

4. Công bố
Tập dữ liệu có thể được công bố cho mục đích nghiên cứu. Khi công bố, bản ghi được
phát hành kèm thông tin hồ sơ ở dạng NHÓM (nhóm tuổi, giới tính, vùng miền), không
kèm họ tên, email hay số điện thoại của tôi.

5. Quyền của tôi
- Tôi được dừng tham gia bất cứ lúc nào, không cần nêu lý do.
- Tôi được yêu cầu gỡ bản ghi của mình khỏi tập dữ liệu chưa công bố.
- Với tập đã công bố, việc thu hồi có giới hạn về mặt kỹ thuật, và điều này được
  nói rõ với tôi trước khi tôi đồng ý.

6. Thời gian lưu trữ
[CẦN ĐIỀN: thời gian lưu dữ liệu và cách xử lý khi hết thời gian đó.]

7. Liên hệ
[CẦN ĐIỀN: tên và cách liên hệ của người chịu trách nhiệm dữ liệu.]

Tôi xác nhận đã đọc, hiểu và đồng ý với các nội dung trên.$consent$,
    CURRENT_DATE,
    TRUE
)
ON CONFLICT (version) DO NOTHING;

-- ------------------------------------------------------------
-- Bù dòng cho những phiên bản ĐÃ có người đồng ý trước khi bảng này ra đời.
--
-- Không bịa nội dung cho chúng: body nói thẳng là nội dung không được lưu lại. Giữ dòng
-- để không mất dấu ai đã đồng ý phiên bản nào, và để khoá ngoại phía dưới gắn được.
-- Nếu ở đây hiện ra những version lạ ('string', 'abc'...) thì đó là dữ liệu rác do
-- endpoint cũ nhận chuỗi tự do — thấy thì dọn, đừng bỏ qua.
-- ------------------------------------------------------------
INSERT INTO consent_document (version, title, body, effective_from, is_active)
SELECT DISTINCT
    uc.version,
    'Phiên bản cũ ' || uc.version || ' — nội dung không được lưu lại',
    'Phiên bản này đã có người đồng ý TRƯỚC khi hệ thống bắt đầu lưu nội dung cam kết '
    || '(migration V39). Nội dung lúc đó nằm trong mã nguồn frontend nên không khôi phục '
    || 'được. Dòng này tồn tại để giữ dấu vết ai đã đồng ý phiên bản nào.',
    COALESCE(MIN(uc.accepted_at)::date, CURRENT_DATE),
    FALSE
FROM user_consent uc
WHERE NOT EXISTS (
    SELECT 1 FROM consent_document cd WHERE cd.version = uc.version
)
GROUP BY uc.version;

-- ------------------------------------------------------------
-- Giờ mới gắn khoá ngoại: mọi version trong user_consent đều đã có dòng tương ứng.
--
-- KHÔNG có ON DELETE CASCADE và KHÔNG có SET NULL: bản cam kết đã có người ký thì không
-- được phép xoá. Xoá nó là xoá bằng chứng người ta đã đồng ý điều gì.
-- ------------------------------------------------------------
DO $$ BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'fk_user_consent_document'
    ) THEN
        ALTER TABLE user_consent
            ADD CONSTRAINT fk_user_consent_document
            FOREIGN KEY (version) REFERENCES consent_document(version);
    END IF;
END $$;

COMMIT;
