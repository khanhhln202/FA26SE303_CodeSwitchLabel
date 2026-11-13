-- ============================================================
-- V27 — Chủ đề động: bảng domain thay cho ENUM script_domain
--
-- Admin phải tạo/đổi tên/xóa chủ đề được mà không deploy lại code.
-- ENUM cố định ba giá trị không làm được việc đó, nên domain thành
-- bảng dữ liệu thật. code giữ đúng tên cũ (it_technology ->
-- ItTechnology...) để payload JSON hiện có vẫn hợp lệ.
--
-- GIỚI HẠN CÓ CHỦ Ý: script_id chỉ có một chữ số cho domain (digit 2),
-- nên tối đa 9 domain kể cả domain đã xóa mềm. Chữ số không tái sử
-- dụng vì mã câu cũ vẫn mang nó.
--
-- LƯU Ý TRIỂN KHAI: migration này đổi kiểu cột và DROP ENUM, nên code
-- cũ không chạy được sau khi áp. Phải dừng API trước khi chạy
-- (deploy.sh áp migration trước khi dựng container mới).
--
-- Chạy một lần; toàn bộ nằm trong một transaction.
-- ============================================================

BEGIN;

-- ------------------------------------------------------------
-- 1. Bảng domain + ba chủ đề gốc
-- ------------------------------------------------------------
CREATE TABLE domain (
    code       TEXT PRIMARY KEY,                  -- bất biến, vd. ItTechnology
    name       TEXT NOT NULL,                     -- đổi được, vd. Công nghệ thông tin
    id_digit   CHAR(1) NOT NULL UNIQUE CHECK (id_digit BETWEEN '1' AND '9'),
    is_active  BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE UNIQUE INDEX uq_domain_active_name ON domain (lower(name)) WHERE is_active;

CREATE OR REPLACE FUNCTION fn_domain_immutable_columns() RETURNS trigger AS $$ BEGIN
    IF NEW.code IS DISTINCT FROM OLD.code THEN
        RAISE EXCEPTION 'domain.code is immutable';
    END IF;
    IF NEW.id_digit IS DISTINCT FROM OLD.id_digit THEN
        RAISE EXCEPTION 'domain.id_digit is immutable';
    END IF;
    RETURN NEW;
END $$ LANGUAGE plpgsql;

CREATE TRIGGER trg_domain_immutable
    BEFORE UPDATE OF code, id_digit ON domain
    FOR EACH ROW EXECUTE FUNCTION fn_domain_immutable_columns();

INSERT INTO domain (code, name, id_digit) VALUES
    ('ItTechnology', 'Công nghệ thông tin', '1'),
    ('Education',    'Giáo dục',             '2'),
    ('DailyLife',    'Hội thoại hàng ngày',  '3');

-- ------------------------------------------------------------
-- 2. View/trigger phụ thuộc cột domain phải bỏ trước khi đổi kiểu cột
-- ------------------------------------------------------------
DROP VIEW IF EXISTS v_task_progress;
DROP VIEW IF EXISTS v_wave_progress;

-- Trigger có danh sách cột UPDATE OF domain cũng bị coi là phụ thuộc cột.
DROP TRIGGER IF EXISTS trg_script_consistency ON script;
DROP TRIGGER IF EXISTS trg_task_wave_domain ON task;
DROP TRIGGER IF EXISTS trg_wave_domain_change ON wave;

-- ------------------------------------------------------------
-- 3. Enum -> TEXT, map giá trị cũ sang code mới
-- ------------------------------------------------------------
ALTER TABLE script DROP CONSTRAINT IF EXISTS ck_script_id_domain_digit;

ALTER TABLE script
    ALTER COLUMN domain TYPE TEXT
    USING (CASE domain::text
               WHEN 'it_technology' THEN 'ItTechnology'
               WHEN 'education'     THEN 'Education'
               WHEN 'daily_life'    THEN 'DailyLife'
           END);
ALTER TABLE script
    ADD CONSTRAINT fk_script_domain FOREIGN KEY (domain) REFERENCES domain(code);

ALTER TABLE user_domain
    ALTER COLUMN domain TYPE TEXT
    USING (CASE domain::text
               WHEN 'it_technology' THEN 'ItTechnology'
               WHEN 'education'     THEN 'Education'
               WHEN 'daily_life'    THEN 'DailyLife'
           END);
ALTER TABLE user_domain
    ADD CONSTRAINT fk_user_domain_domain FOREIGN KEY (domain) REFERENCES domain(code);

ALTER TABLE wave
    ALTER COLUMN domain TYPE TEXT
    USING (CASE domain::text
               WHEN 'it_technology' THEN 'ItTechnology'
               WHEN 'education'     THEN 'Education'
               WHEN 'daily_life'    THEN 'DailyLife'
           END);
ALTER TABLE wave
    ADD CONSTRAINT fk_wave_domain FOREIGN KEY (domain) REFERENCES domain(code);

ALTER TABLE task
    ALTER COLUMN domain TYPE TEXT
    USING (CASE domain::text
               WHEN 'it_technology' THEN 'ItTechnology'
               WHEN 'education'     THEN 'Education'
               WHEN 'daily_life'    THEN 'DailyLife'
           END);
ALTER TABLE task
    ADD CONSTRAINT fk_task_domain FOREIGN KEY (domain) REFERENCES domain(code);

-- ------------------------------------------------------------
-- 4. Hàm/trigger đổi kiểu tham số và kiểm tra chữ số domain
-- ------------------------------------------------------------
DROP FUNCTION IF EXISTS fn_generate_script_id(INT, script_domain, INT);

CREATE OR REPLACE FUNCTION fn_generate_script_id(
    p_en_word_count INT,            -- 1..9
    p_domain        TEXT,           -- domain.code, phải đang active
    p_relation_code INT             -- 1 = direct translation, 2 = proper noun
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

    SELECT id_digit INTO v_domain_digit
      FROM domain
     WHERE code = p_domain AND is_active;
    IF NOT FOUND THEN
        RAISE EXCEPTION 'unknown or inactive script domain %', p_domain;
    END IF;

    IF p_en_word_count IN (1, 2) AND v_domain_digit IN ('1', '2', '3') THEN
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

CREATE OR REPLACE FUNCTION fn_validate_task_wave_domain() RETURNS trigger AS $$
DECLARE
    v_wave_domain TEXT;
BEGIN
    -- Detached task: any domain is fine.
    IF NEW.wave_id IS NULL THEN
        RETURN NEW;
    END IF;

    SELECT c.domain
      INTO v_wave_domain
      FROM wave c
     WHERE c.wave_id = NEW.wave_id;

    IF NOT FOUND THEN
        RAISE EXCEPTION 'wave % does not exist', NEW.wave_id;
    END IF;

    IF NEW.domain IS DISTINCT FROM v_wave_domain THEN
        RAISE EXCEPTION
            'task % domain % must match wave % domain %',
            NEW.task_id, NEW.domain, NEW.wave_id, v_wave_domain;
    END IF;

    RETURN NEW;
END $$ LANGUAGE plpgsql;

CREATE TRIGGER trg_task_wave_domain
    BEFORE INSERT OR UPDATE OF wave_id, domain ON task
    FOR EACH ROW EXECUTE FUNCTION fn_validate_task_wave_domain();

CREATE TRIGGER trg_wave_domain_change
    BEFORE UPDATE OF domain ON wave
    FOR EACH ROW EXECUTE FUNCTION fn_validate_wave_domain_change();

CREATE OR REPLACE FUNCTION fn_script_word_consistency() RETURNS trigger AS $$
DECLARE
    v_script_id VARCHAR(16);
    v_en_count  INT;
    v_domain_digit CHAR(1);
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

    SELECT s.en_word_count, d.id_digit
      INTO v_en_count, v_domain_digit
      FROM script s
      JOIN domain d ON d.code = s.domain
     WHERE s.script_id = v_script_id;
    IF NOT FOUND THEN
        RETURN NULL;   -- script was deleted (words cascade); nothing to check
    END IF;

    IF substr(v_script_id, 4, 1) <> v_domain_digit THEN
        RAISE EXCEPTION 'script %: id digit 2 must be % (domain digit)',
            v_script_id, v_domain_digit;
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

DROP TRIGGER IF EXISTS trg_script_consistency ON script;
CREATE CONSTRAINT TRIGGER trg_script_consistency
    AFTER INSERT OR UPDATE OF en_word_count, domain ON script
    DEFERRABLE INITIALLY DEFERRED
    FOR EACH ROW EXECUTE FUNCTION fn_script_word_consistency();

CREATE OR REPLACE FUNCTION fn_validate_task_script() RETURNS trigger AS $$
DECLARE
    v_task_type     task_type;
    v_task_domain   TEXT;
    v_script_domain TEXT;
    v_script_status script_status;
BEGIN
    SELECT task_type, domain INTO v_task_type, v_task_domain FROM task WHERE task_id = NEW.task_id;
    IF NOT FOUND THEN
        RAISE EXCEPTION 'task % does not exist', NEW.task_id;
    END IF;

    IF v_task_type <> 'recording' THEN
        RAISE EXCEPTION 'cannot attach script % to non-recording task % (task_type: %)',
            NEW.script_id, NEW.task_id, v_task_type;
    END IF;

    SELECT domain, status INTO v_script_domain, v_script_status FROM script WHERE script_id = NEW.script_id;
    IF NOT FOUND THEN
        RAISE EXCEPTION 'script % does not exist', NEW.script_id;
    END IF;

    IF v_task_domain <> v_script_domain THEN
        RAISE EXCEPTION 'task % domain (%) does not match script % domain (%)',
            NEW.task_id, v_task_domain, NEW.script_id, v_script_domain;
    END IF;

    IF v_script_status <> 'validated' THEN
        RAISE EXCEPTION 'script % must be validated to attach to recording task % (status: %)',
            NEW.script_id, NEW.task_id, v_script_status;
    END IF;

    RETURN NEW;
END $$ LANGUAGE plpgsql;

DROP TYPE script_domain;

-- ------------------------------------------------------------
-- 5. Tạo lại view đã bỏ ở bước 2
-- ------------------------------------------------------------
CREATE VIEW v_task_progress AS
SELECT
    t.task_id,
    t.wave_id,
    c.wave_name,
    t.task_type,
    t.domain,
    t.status        AS task_status,
    t.target_qty,
    t.deadline,
    -- một task có thể giao cho nhiều người. Chỉ tiêu là của cả nhóm nên
    -- không nhân theo số này — chỉ để báo cáo biết đang chia việc cho bao nhiêu người.
    (SELECT COUNT(*) FROM task_assignment a
        WHERE a.task_id = t.task_id AND a.assignment_status IN ('active', 'completed')) AS assignee_count,
    (SELECT COUNT(*) FROM task_script ts WHERE ts.task_id = t.task_id) AS scripts_assigned,
    (SELECT COUNT(*) FROM task_script ts
        WHERE ts.task_id = t.task_id AND ts.status = 'completed')      AS scripts_completed,
    (SELECT COUNT(*) FROM review rv WHERE rv.task_id = t.task_id)      AS reviews_done
FROM task t
LEFT JOIN wave c ON c.wave_id = t.wave_id;

CREATE VIEW v_wave_progress AS
SELECT
    c.wave_id,
    c.wave_name,
    c.domain,
    c.target_qty AS wave_target_qty,
    c.start_date,
    c.end_date,
    c.status AS wave_status,
    c.assigned_to,
    COALESCE(SUM(t.target_qty), 0) AS allocated_task_qty,
    c.target_qty - COALESCE(SUM(t.target_qty), 0) AS remaining_task_qty,
    COUNT(t.task_id) AS task_count,
    COUNT(t.task_id) FILTER (WHERE t.status = 'completed') AS completed_task_count
FROM wave c
LEFT JOIN task t ON t.wave_id = c.wave_id
GROUP BY
    c.wave_id,
    c.wave_name,
    c.domain,
    c.target_qty,
    c.start_date,
    c.end_date,
    c.status,
    c.assigned_to;

COMMIT;
