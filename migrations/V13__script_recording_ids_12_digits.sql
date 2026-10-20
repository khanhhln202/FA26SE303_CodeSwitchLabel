-- ============================================================
-- V13 — Script/recording ID mới: 12 chữ số, 13 bộ đếm tuần tự
--
-- script_id mới = s_ + 3 chữ số nghĩa + 9 chữ số thứ tự (tổng 12 chữ số):
--   digit 1 = số từ tiếng Anh (1 hoặc 2; > 2 dùng bộ đếm chung)
--   digit 2 = chủ đề (1 IT / 2 education / 3 daily life)
--   digit 3 = quan hệ Anh–Việt (1 dịch nghĩa / 2 danh từ riêng)
--   9 chữ số cuối = serial tuần tự của đúng bộ đếm.
--
-- 13 bộ đếm:
--   12 = (en_word_count 1/2) x (3 chủ đề) x (2 quan hệ)
--   1  = fallback cho mọi câu có en_word_count > 2.
--
-- recording_id = r_cs_/r_vi_ + đúng 12 chữ số của script; thu lại thêm _tN.
-- ID cũ 9 chữ số vẫn hợp lệ và KHÔNG bị đổi. Vì ID mới dài hơn nên không thể
-- trùng ID cũ, không cần migrate dữ liệu.
--
-- Idempotent: chạy lại nhiều lần vẫn cho kết quả như nhau.
-- ============================================================

BEGIN;

-- Hàm cũ trả VARCHAR(11)/VARCHAR(20); PostgreSQL không cho đổi return type
-- bằng CREATE OR REPLACE nên phải drop trước.
DROP FUNCTION IF EXISTS fn_generate_script_id(INT, script_domain, INT);
DROP FUNCTION IF EXISTS fn_generate_recording_id(VARCHAR, sentence_variant, INT);

-- ------------------------------------------------------------
-- 1. views phụ thuộc vào cột ID phải tháo trước khi nới kiểu,
--    rồi tạo lại sau khi ràng buộc/hàm đã được cập nhật.
-- ------------------------------------------------------------
DROP VIEW IF EXISTS v_dataset_missing_pair;
DROP VIEW IF EXISTS v_speaker_performance;

-- ------------------------------------------------------------
-- 2. Nới cột. FK là varchar với varchar nên chỉ tăng typmod là an toàn.
-- ------------------------------------------------------------
ALTER TABLE task_recording   ALTER COLUMN recording_id TYPE VARCHAR(32);
ALTER TABLE review           ALTER COLUMN recording_id TYPE VARCHAR(32);
ALTER TABLE dataset_recording ALTER COLUMN recording_id TYPE VARCHAR(32);
ALTER TABLE recording        ALTER COLUMN recording_id TYPE VARCHAR(32);
ALTER TABLE recording        ALTER COLUMN script_id    TYPE VARCHAR(16);

-- trg_task_script_validation liệt kê cột script_id trong mệnh đề OF, nên
-- PostgreSQL không cho nới kiểu cột khi trigger còn tồn tại.
DROP TRIGGER IF EXISTS trg_task_script_validation ON task_script;

ALTER TABLE script_word      ALTER COLUMN script_id TYPE VARCHAR(16);
ALTER TABLE script_review    ALTER COLUMN script_id TYPE VARCHAR(16);
ALTER TABLE script_proposal  ALTER COLUMN script_id TYPE VARCHAR(16);
ALTER TABLE task_script      ALTER COLUMN script_id TYPE VARCHAR(16);
ALTER TABLE script_queue     ALTER COLUMN script_id TYPE VARCHAR(16);
ALTER TABLE script           ALTER COLUMN script_id TYPE VARCHAR(16);

ALTER TABLE audit_log        ALTER COLUMN entity_id TYPE VARCHAR(32);

-- ------------------------------------------------------------
-- 2. Thay constraint định dạng, chấp nhận cả ID cũ 9 số và mới 12 số.
-- ------------------------------------------------------------
ALTER TABLE script DROP CONSTRAINT IF EXISTS ck_script_id_format;
ALTER TABLE script ADD CONSTRAINT ck_script_id_format
    CHECK (script_id ~ '^s_([0-9]{9}|[0-9]{12})$');

ALTER TABLE recording DROP CONSTRAINT IF EXISTS ck_recording_id_format;
ALTER TABLE recording DROP CONSTRAINT IF EXISTS ck_recording_script_serial;

ALTER TABLE recording ADD CONSTRAINT ck_recording_id_format CHECK
    (recording_id ~ '^r_(cs|vi)_([0-9]{9}|[0-9]{12})(_t([2-9]|[1-9][0-9]+))?$');

ALTER TABLE recording ADD CONSTRAINT ck_recording_script_serial CHECK
    (regexp_replace(recording_id, '^r_(cs|vi)_([0-9]{9}|[0-9]{12})(_t([2-9]|[1-9][0-9]+))?$', '\2')
     = substr(script_id, 3));

-- ------------------------------------------------------------
-- 3. 13 bộ đếm. Serial độc lập từng bộ nên ID vẫn tăng dần, không random.
-- ------------------------------------------------------------
CREATE SEQUENCE IF NOT EXISTS script_serial_1_1_1 AS BIGINT START WITH 1;
CREATE SEQUENCE IF NOT EXISTS script_serial_1_1_2 AS BIGINT START WITH 1;
CREATE SEQUENCE IF NOT EXISTS script_serial_1_2_1 AS BIGINT START WITH 1;
CREATE SEQUENCE IF NOT EXISTS script_serial_1_2_2 AS BIGINT START WITH 1;
CREATE SEQUENCE IF NOT EXISTS script_serial_1_3_1 AS BIGINT START WITH 1;
CREATE SEQUENCE IF NOT EXISTS script_serial_1_3_2 AS BIGINT START WITH 1;
CREATE SEQUENCE IF NOT EXISTS script_serial_2_1_1 AS BIGINT START WITH 1;
CREATE SEQUENCE IF NOT EXISTS script_serial_2_1_2 AS BIGINT START WITH 1;
CREATE SEQUENCE IF NOT EXISTS script_serial_2_2_1 AS BIGINT START WITH 1;
CREATE SEQUENCE IF NOT EXISTS script_serial_2_2_2 AS BIGINT START WITH 1;
CREATE SEQUENCE IF NOT EXISTS script_serial_2_3_1 AS BIGINT START WITH 1;
CREATE SEQUENCE IF NOT EXISTS script_serial_2_3_2 AS BIGINT START WITH 1;
CREATE SEQUENCE IF NOT EXISTS script_serial_other AS BIGINT START WITH 1;

-- ------------------------------------------------------------
-- 4. Hàm sinh ID mới.
-- ------------------------------------------------------------
CREATE OR REPLACE FUNCTION fn_generate_script_id(
    p_en_word_count INT,
    p_domain        script_domain,
    p_relation_code INT
) RETURNS VARCHAR(16) AS $$
DECLARE
    v_domain_digit CHAR(1);
    v_serial       BIGINT;
BEGIN
    IF p_en_word_count NOT BETWEEN 1 AND 9 THEN
        RAISE EXCEPTION 'en_word_count must be 1..9 (single ID digit)';
    END IF;
    IF p_relation_code NOT IN (1, 2) THEN
        RAISE EXCEPTION 'relation_code must be 1 (direct translation) or 2 (proper noun)';
    END IF;

    v_domain_digit := CASE p_domain WHEN 'it_technology' THEN '1'
                                    WHEN 'education'     THEN '2'
                                    WHEN 'daily_life'    THEN '3' END;
    IF v_domain_digit IS NULL THEN
        RAISE EXCEPTION 'unknown script domain %', p_domain;
    END IF;

    IF p_en_word_count IN (1, 2) THEN
        v_serial := nextval(format('script_serial_%s_%s_%s',
            p_en_word_count, v_domain_digit, p_relation_code)::regclass);
    ELSE
        v_serial := nextval('script_serial_other'::regclass);
    END IF;

    IF v_serial > 999999999 THEN
        RAISE EXCEPTION 'script serial counter exhausted 9 digits for en_word_count %', p_en_word_count;
    END IF;

    RETURN 's_'
        || p_en_word_count::text
        || v_domain_digit
        || p_relation_code::text
        || lpad(v_serial::text, 9, '0');
END $$ LANGUAGE plpgsql;

CREATE OR REPLACE FUNCTION fn_generate_recording_id(
    p_script_id VARCHAR(16),
    p_variant   sentence_variant,
    p_take      INT DEFAULT 1
) RETURNS VARCHAR(32) AS $$ BEGIN
    IF p_script_id !~ '^s_([0-9]{9}|[0-9]{12})$' THEN
        RAISE EXCEPTION 'invalid script_id %', p_script_id;
    END IF;
    IF p_take < 1 THEN
        RAISE EXCEPTION 'take must be >= 1';
    END IF;
    RETURN 'r_'
        || (CASE p_variant WHEN 'code_switching'   THEN 'cs'
                           WHEN 'pure_vietnamese' THEN 'vi' END)
        || '_'
        || substr(p_script_id, 3)
        || (CASE WHEN p_take > 1 THEN '_t' || p_take::text ELSE '' END);
END $$ LANGUAGE plpgsql;

-- Trigger kiểm tra digit 1/3 của script_id (deferred). Chỉ đổi kiểu biến
-- để chứa được ID 14 ký tự.
CREATE OR REPLACE FUNCTION fn_script_word_consistency() RETURNS trigger AS $$
DECLARE
    v_script_id VARCHAR(16);
    v_en_count  INT;
    v_has_proper BOOLEAN;
    v_expected_relation CHAR(1);
BEGIN
    IF TG_TABLE_NAME = 'script' THEN
        v_script_id := NEW.script_id;
    ELSIF TG_OP = 'DELETE' THEN
        v_script_id := OLD.script_id;
    ELSE
        v_script_id := NEW.script_id;
    END IF;

    SELECT en_word_count INTO v_en_count FROM script WHERE script_id = v_script_id;
    IF NOT FOUND THEN
        RETURN NULL;
    END IF;

    IF (SELECT COUNT(*) FROM script_word WHERE script_id = v_script_id) <> v_en_count THEN
        RAISE EXCEPTION 'script %: en_word_count % does not match number of script_word rows',
            v_script_id, v_en_count;
    END IF;

    SELECT COALESCE(bool_or(relation = 'proper_noun'), FALSE)
      INTO v_has_proper
      FROM script_word WHERE script_id = v_script_id;
    v_expected_relation := CASE WHEN v_has_proper THEN '2' ELSE '1' END;

    IF substr(v_script_id, 5, 1) <> v_expected_relation THEN
        RAISE EXCEPTION 'script %: id digit 3 must be % (relation derived from script_word)',
            v_script_id, v_expected_relation;
    END IF;
    RETURN NULL;
END $$ LANGUAGE plpgsql;

-- ------------------------------------------------------------
-- 4b. Tạo lại trigger bị tháo do cột ID trong mệnh đề OF.
-- ------------------------------------------------------------
CREATE TRIGGER trg_task_script_validation
    BEFORE INSERT OR UPDATE OF task_id, script_id ON task_script
    FOR EACH ROW EXECUTE FUNCTION fn_validate_task_script();

-- ------------------------------------------------------------
-- 5. Tạo lại view đã tháo với định nghĩa gốc (không thay đổi cột đầu ra).
-- ------------------------------------------------------------
CREATE VIEW v_speaker_performance AS
SELECT
    u.user_id,
    u.full_name,
    COUNT(r.recording_id)                                        AS recordings,
    COUNT(*) FILTER (WHERE r.status = 'approved')                AS approved,
    COUNT(*) FILTER (WHERE r.status = 'rejected')                AS rejected,
    ROUND(100.0 * COUNT(*) FILTER (WHERE r.status = 'approved')
          / NULLIF(COUNT(*) FILTER (WHERE r.status IN ('approved', 'rejected')), 0), 1)
                                                                  AS approval_rate_pct
FROM app_user u
JOIN recording r ON r.speaker_id = u.user_id
GROUP BY u.user_id, u.full_name;

CREATE VIEW v_dataset_missing_pair AS
SELECT
    dr.dataset_id,
    r.script_id,
    COUNT(*) FILTER (WHERE r.sentence_variant = 'code_switching')   AS has_cs,
    COUNT(*) FILTER (WHERE r.sentence_variant = 'pure_vietnamese') AS has_vi
FROM dataset_recording dr
JOIN recording r ON r.recording_id = dr.recording_id
GROUP BY dr.dataset_id, r.script_id
HAVING COUNT(*) FILTER (WHERE r.sentence_variant = 'code_switching') = 0
    OR COUNT(*) FILTER (WHERE r.sentence_variant = 'pure_vietnamese') = 0;

COMMIT;
