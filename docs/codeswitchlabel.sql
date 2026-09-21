-- ============================================================
-- CodeSwitchLabel — Speech Data Collection & QC System
-- PostgreSQL schema (v11+, 13+ recommended)
-- Run: psql -d codeswitchlabel -f codeswitchlabel_schema.sql
-- ============================================================

BEGIN;

-- ============================================================
-- 1. ENUM TYPES
-- ============================================================
CREATE TYPE user_status           AS ENUM ('active','inactive');
CREATE TYPE occupation            AS ENUM ('student','employed','other');
CREATE TYPE config_value_type     AS ENUM ('int','bool','string');
CREATE TYPE audit_action          AS ENUM ('create','update','delete','import','export','assign','release','login');
CREATE TYPE script_status         AS ENUM ('pending_validation','validated','rejected','deactivated');
CREATE TYPE script_domain         AS ENUM ('it_technology','education','daily_life');
CREATE TYPE script_review_action  AS ENUM ('accepted','edited','rejected');
CREATE TYPE task_type             AS ENUM ('recording','review');
CREATE TYPE task_status           AS ENUM ('draft','open','in_progress','completed','cancelled');
CREATE TYPE assignment_status     AS ENUM ('active','completed','reassigned','cancelled');
CREATE TYPE task_script_status    AS ENUM ('pending','completed','rejected');
CREATE TYPE sentence_variant      AS ENUM ('code_switching','pure_vietnamese');
CREATE TYPE recording_status      AS ENUM ('qc_failed','pending_review','approved','rejected');
CREATE TYPE task_recording_status AS ENUM ('queued','reviewed','skipped');
CREATE TYPE review_decision       AS ENUM ('approved','rejected');
CREATE TYPE rejection_category    AS ENUM ('content','audio_quality','pronunciation','other');
CREATE TYPE dataset_status        AS ENUM ('draft','released','archived');
CREATE TYPE dataset_file_format   AS ENUM ('json','csv');

-- ============================================================
-- 2. SEQUENCES
-- ============================================================
-- audit_id: plain sequence (not IDENTITY) so it also works on
-- PostgreSQL < 17, where partitioned tables cannot own identity columns.
CREATE SEQUENCE audit_log_audit_id_seq;

-- Global serial for digits 4-9 of script_id (sequential, not random).
CREATE SEQUENCE script_serial_seq AS BIGINT START WITH 1;

-- ============================================================
-- 3. TABLES  (FK dependency order)
-- ============================================================

-- ------------------------------------------------------------
-- USER / ACCESS
-- ------------------------------------------------------------
CREATE TABLE role (
    role_id     SMALLINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    role_name   VARCHAR(32)  NOT NULL UNIQUE,      -- [ERD] UNIQUE(role_name)
    description TEXT
);

CREATE TABLE app_user (
    user_id       BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    role_id       SMALLINT NOT NULL REFERENCES role(role_id),
    full_name     VARCHAR(255) NOT NULL,
    email         VARCHAR(255) NOT NULL UNIQUE,    -- [ERD] UNIQUE(email)
    phone         VARCHAR(32),
    password_hash VARCHAR(60)  NOT NULL,           -- bcrypt output = 60 chars
    status        user_status NOT NULL DEFAULT 'active',
    created_at    TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE speaker_profile (                    -- 1 : 0..1 with app_user
    user_id       BIGINT PRIMARY KEY REFERENCES app_user(user_id) ON DELETE CASCADE,
    birth_year    SMALLINT CHECK (birth_year BETWEEN 1900 AND 2100),
    province      VARCHAR(100),                   -- region (N/C/S) auto-derived, not stored
    english_level NUMERIC(2,1) CHECK (english_level BETWEEN 0.0 AND 9.0),  -- IELTS band
    occupation    occupation,
    major         VARCHAR(100)                    -- chuyên ngành: IT | english | business ...
);

CREATE TABLE system_config (
    config_id    SMALLINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    config_key   VARCHAR(100) NOT NULL UNIQUE,
    config_value TEXT NOT NULL,
    value_type   config_value_type NOT NULL DEFAULT 'string',
    description  TEXT,
    updated_by   BIGINT REFERENCES app_user(user_id) ON DELETE SET NULL,  -- NULL = system
    updated_at   TIMESTAMPTZ NOT NULL DEFAULT now()
);

-- Partitioned audit log: PK must contain the partition key.
CREATE TABLE audit_log (
    audit_id    BIGINT NOT NULL DEFAULT nextval('audit_log_audit_id_seq'),
    changed_at  TIMESTAMPTZ NOT NULL DEFAULT now(),
    user_id     BIGINT REFERENCES app_user(user_id) ON DELETE SET NULL,  -- NULL = system action
    entity_type VARCHAR(50) NOT NULL,
    entity_id   VARCHAR(20) NOT NULL,   -- polymorphic: numeric ID or s_/r_ code
    action      audit_action NOT NULL,
    old_value   JSONB,
    new_value   JSONB,
    PRIMARY KEY (audit_id, changed_at)
) PARTITION BY RANGE (changed_at);

CREATE INDEX idx_audit_user      ON audit_log (user_id);
CREATE INDEX idx_audit_entity    ON audit_log (entity_type, entity_id);

-- ------------------------------------------------------------
-- IMPORT / SCRIPT
-- ------------------------------------------------------------
CREATE TABLE import_batch (
    batch_id     BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    imported_by  BIGINT NOT NULL REFERENCES app_user(user_id),
    file_name    TEXT NOT NULL,
    script_count INT NOT NULL DEFAULT 0,           -- counts sentence pairs, not file rows
    created_at   TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT ck_import_batch_count CHECK (script_count >= 0)
);

CREATE TABLE script (
    script_id       VARCHAR(11) PRIMARY KEY,       -- natural key: s_ + 9 meaningful digits
    cs_content      TEXT NOT NULL,                 -- code-switching sentence, KEEPS the [vi]/[en] tags
    ve_content      TEXT NOT NULL,                 -- pure Vietnamese equivalent
    -- Word-level mapping from input_text.json, e.g. [{"source":"scan","target":"quét",...}].
    -- Kept as JSONB because SCRIPT_EN_WORD was dropped: without this column the alignment
    -- the teacher provides would be lost right after import.
    alignment       JSONB,
    status          script_status NOT NULL DEFAULT 'pending_validation',
    word_count      INT NOT NULL DEFAULT 0,
    en_word_count   INT NOT NULL DEFAULT 0,
    domain          script_domain NOT NULL,
    created_by      BIGINT NOT NULL REFERENCES app_user(user_id),
    import_batch_id BIGINT REFERENCES import_batch(batch_id),  -- NULL = manual entry
    created_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT ck_script_id_format  CHECK (script_id ~ '^s_[0-9]{9}$'),
    -- ID digits must stay meaningful (teacher's rule):
    --   digit 1 (pos 3) = number of embedded English words
    --   digit 2 (pos 4) = domain code (1 IT / 2 education / 3 daily life)
    --   digit 3 (pos 5) = EN-VN relation; no matching column since
    --   SCRIPT_EN_WORD was removed -> enforced only at generation time.
    CONSTRAINT ck_script_id_word_digit   CHECK (substr(script_id, 3, 1) = en_word_count::text),
    CONSTRAINT ck_script_id_domain_digit CHECK (substr(script_id, 4, 1) =
        CASE domain WHEN 'it_technology' THEN '1'
                    WHEN 'education'     THEN '2'
                    WHEN 'daily_life'    THEN '3' END),
    CONSTRAINT ck_script_word_counts CHECK (word_count >= 0
                                        AND en_word_count >= 0
                                        AND en_word_count <= word_count)
);

CREATE INDEX idx_script_status       ON script (status);        -- review/record queues
CREATE INDEX idx_script_domain       ON script (domain);
CREATE INDEX idx_script_created_by   ON script (created_by);
CREATE INDEX idx_script_import_batch ON script (import_batch_id);

CREATE TABLE script_error_reason (
    reason_id   SMALLINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    reason_code VARCHAR(50) NOT NULL UNIQUE,
    description TEXT,
    sort_order  INT NOT NULL DEFAULT 0,
    is_active   BOOLEAN NOT NULL DEFAULT TRUE     -- retriable by admin
);

CREATE TABLE script_review (
    script_review_id  BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,  -- surrogate; re-review allowed
    script_id         VARCHAR(11) NOT NULL REFERENCES script(script_id),
    user_id           BIGINT NOT NULL REFERENCES app_user(user_id),
    error_reason_id   SMALLINT REFERENCES script_error_reason(reason_id),
    action            script_review_action NOT NULL,
    edited_cs_content TEXT,
    edited_ve_content TEXT,
    comment           TEXT,
    reviewed_at       TIMESTAMPTZ NOT NULL DEFAULT now(),
    -- edits always apply to the sentence PAIR as a unit
    CONSTRAINT ck_script_review_edited CHECK (action <> 'edited'
        OR (edited_cs_content IS NOT NULL AND edited_ve_content IS NOT NULL))
);

CREATE INDEX idx_script_review_script ON script_review (script_id);
CREATE INDEX idx_script_review_user   ON script_review (user_id);

-- ------------------------------------------------------------
-- TASK
-- ------------------------------------------------------------
CREATE TABLE task (
    task_id     BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    created_by  BIGINT NOT NULL REFERENCES app_user(user_id),
    task_type   task_type NOT NULL,
    description TEXT,
    target_qty  INT NOT NULL CHECK (target_qty > 0),
    deadline    TIMESTAMPTZ,
    status      task_status NOT NULL DEFAULT 'draft',
    created_at  TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE task_assignment (
    assignment_id     BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,  -- surrogate: keeps history
    task_id           BIGINT NOT NULL REFERENCES task(task_id) ON DELETE CASCADE,
    user_id           BIGINT NOT NULL REFERENCES app_user(user_id),
    assigned_at       TIMESTAMPTZ NOT NULL DEFAULT now(),
    assignment_status assignment_status NOT NULL DEFAULT 'active'
);

-- only one ACTIVE assignment per task
CREATE UNIQUE INDEX uq_task_assignment_active
    ON task_assignment (task_id) WHERE assignment_status = 'active';
CREATE INDEX idx_task_assignment_user ON task_assignment (user_id);

CREATE TABLE task_script (
    task_id     BIGINT NOT NULL REFERENCES task(task_id) ON DELETE CASCADE,
    script_id   VARCHAR(11) NOT NULL REFERENCES script(script_id),
    status      task_script_status NOT NULL DEFAULT 'pending',
    included_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    PRIMARY KEY (task_id, script_id)
);
CREATE INDEX idx_task_script_script ON task_script (script_id);

-- ------------------------------------------------------------
-- RECORDING / REVIEW  (the two protagonists)
-- ------------------------------------------------------------
CREATE TABLE recording (
    recording_id     VARCHAR(20) PRIMARY KEY,      -- natural key: r_cs_/r_vi_ + serial (+ _tN)
    sentence_variant sentence_variant NOT NULL,
    script_id        VARCHAR(11) NOT NULL REFERENCES script(script_id),
    speaker_id       BIGINT NOT NULL REFERENCES app_user(user_id),
    task_id          BIGINT REFERENCES task(task_id) ON DELETE SET NULL,  -- NULL = ad-hoc
    cloud_link       VARCHAR(1024) NOT NULL UNIQUE,   -- 3rd-party object storage link
    audio_format     VARCHAR(10) NOT NULL DEFAULT 'wav',
    status           recording_status NOT NULL DEFAULT 'pending_review',
    duration_sec     NUMERIC(8,2) NOT NULL CHECK (duration_sec > 0),
    recorded_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT ck_recording_id_format CHECK
        (recording_id ~ '^r_(cs|vi)_[0-9]{9}(_t[2-9][0-9]*)?$'),
    -- sentence_variant must mirror the prefix
    CONSTRAINT ck_recording_variant_prefix CHECK (
        (sentence_variant = 'code_switching'   AND recording_id ~ '^r_cs_') OR
        (sentence_variant = 'pure_vietnamese' AND recording_id ~ '^r_vi_')),
    -- the 9-digit serial inside recording_id must equal the script's serial
    CONSTRAINT ck_recording_script_serial CHECK
        (regexp_replace(recording_id, '^r_(cs|vi)_([0-9]{9})(_t[2-9][0-9]*)?$', '\2')
         = substr(script_id, 3))
);

CREATE INDEX idx_recording_script  ON recording (script_id);
CREATE INDEX idx_recording_speaker ON recording (speaker_id);
CREATE INDEX idx_recording_task    ON recording (task_id);
CREATE INDEX idx_recording_status  ON recording (status);

-- at most ONE approved recording per (script, variant) — re-records are extra takes
CREATE UNIQUE INDEX uq_recording_approved_pair
    ON recording (script_id, sentence_variant) WHERE status = 'approved';

CREATE TABLE task_recording (
    task_id      BIGINT NOT NULL REFERENCES task(task_id) ON DELETE CASCADE,
    recording_id VARCHAR(20) NOT NULL REFERENCES recording(recording_id),
    status       task_recording_status NOT NULL DEFAULT 'queued',
    included_at  TIMESTAMPTZ NOT NULL DEFAULT now(),
    PRIMARY KEY (task_id, recording_id)
);
CREATE INDEX idx_task_recording_recording ON task_recording (recording_id);

CREATE TABLE rejection_reason (
    reason_id   SMALLINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    reason_code VARCHAR(50) NOT NULL UNIQUE,
    category    rejection_category NOT NULL,
    description TEXT,
    is_active   BOOLEAN NOT NULL DEFAULT TRUE
);

CREATE TABLE review (
    review_id    BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    recording_id VARCHAR(20) NOT NULL REFERENCES recording(recording_id),
    reviewer_id  BIGINT NOT NULL REFERENCES app_user(user_id),
    task_id      BIGINT REFERENCES task(task_id) ON DELETE SET NULL,   -- NULL = spot-check
    review_round SMALLINT NOT NULL CHECK (review_round BETWEEN 1 AND 3),
    decision     review_decision NOT NULL,
    is_blind     BOOLEAN NOT NULL DEFAULT TRUE,
    comment      TEXT,
    reviewed_at  TIMESTAMPTZ NOT NULL DEFAULT now(),
    UNIQUE (recording_id, review_round),   -- one reviewer per slot
    UNIQUE (recording_id, reviewer_id)     -- same reviewer cannot review twice
);
CREATE INDEX idx_review_reviewer ON review (reviewer_id);
CREATE INDEX idx_review_task     ON review (task_id);

CREATE TABLE review_rejection_reason (
    review_id BIGINT NOT NULL REFERENCES review(review_id) ON DELETE CASCADE,
    reason_id SMALLINT NOT NULL REFERENCES rejection_reason(reason_id),
    PRIMARY KEY (review_id, reason_id)
);

-- ------------------------------------------------------------
-- DATASET
-- ------------------------------------------------------------
CREATE TABLE dataset (
    dataset_id      BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    dataset_name    VARCHAR(100) NOT NULL,
    version         VARCHAR(32) NOT NULL,
    description     TEXT,
    filter_criteria JSONB,                          -- reproducible selection
    status          dataset_status NOT NULL DEFAULT 'draft',
    released_by     BIGINT REFERENCES app_user(user_id),      -- NULL until released
    released_at     TIMESTAMPTZ,                             -- NULL until released
    file_key        VARCHAR(255) UNIQUE,                     -- generated on release
    file_format     dataset_file_format,
    recording_count INT NOT NULL DEFAULT 0 CHECK (recording_count >= 0),
    created_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
    UNIQUE (dataset_name, version),
    CONSTRAINT ck_dataset_release CHECK (status <> 'released'
        OR (released_by IS NOT NULL AND released_at IS NOT NULL
            AND file_key IS NOT NULL AND file_format IS NOT NULL))
);

CREATE INDEX idx_dataset_filter ON dataset USING GIN (filter_criteria);

CREATE TABLE dataset_recording (
    dataset_id   BIGINT NOT NULL REFERENCES dataset(dataset_id) ON DELETE CASCADE,
    recording_id VARCHAR(20) NOT NULL REFERENCES recording(recording_id),
    included_at  TIMESTAMPTZ NOT NULL DEFAULT now(),
    PRIMARY KEY (dataset_id, recording_id)
);
CREATE INDEX idx_dataset_recording_recording ON dataset_recording (recording_id);

-- ============================================================
-- 4. AUDIT_LOG MONTHLY PARTITIONS (2025-01 .. 2028-12)
--    Extend yearly, or manage with pg_partman / cron.
--
--    A row whose changed_at has no partition makes the INSERT fail, and because fn_audit
--    runs inside the business transaction, the whole business operation fails with it.
--    Two safeguards: four years of monthly partitions, plus a DEFAULT partition that
--    catches anything outside that range.
-- ============================================================
DO $$ DECLARE
    d DATE;
BEGIN
    FOR i IN 0..47 LOOP
        d := DATE '2025-01-01' + (i * INTERVAL '1 month');
        EXECUTE format(
            'CREATE TABLE IF NOT EXISTS audit_log_%s PARTITION OF audit_log
             FOR VALUES FROM (%L) TO (%L)',
            to_char(d, 'YYYY_MM'), d, d + INTERVAL '1 month');
    END LOOP;
END $$;

CREATE TABLE IF NOT EXISTS audit_log_default PARTITION OF audit_log DEFAULT;

-- ============================================================
-- 5. TRIGGER FUNCTIONS  (rules the ERD marks "trigger-enforced")
-- ============================================================

-- auto-touch updated_at
CREATE OR REPLACE FUNCTION fn_touch_updated_at() RETURNS trigger AS $$ BEGIN
    NEW.updated_at := now();
    RETURN NEW;
END $$ LANGUAGE plpgsql;

CREATE TRIGGER trg_script_touch        BEFORE UPDATE ON script
    FOR EACH ROW EXECUTE FUNCTION fn_touch_updated_at();
CREATE TRIGGER trg_system_config_touch BEFORE UPDATE ON system_config
    FOR EACH ROW EXECUTE FUNCTION fn_touch_updated_at();

-- [ERD] reviewer_id must NOT equal recording's speaker_id
CREATE OR REPLACE FUNCTION fn_review_no_self_review() RETURNS trigger AS $$ DECLARE
    v_speaker BIGINT;
BEGIN
    SELECT speaker_id INTO v_speaker FROM recording WHERE recording_id = NEW.recording_id;
    IF v_speaker = NEW.reviewer_id THEN
        RAISE EXCEPTION 'self-review blocked: user % is the speaker of %', NEW.reviewer_id, NEW.recording_id;
    END IF;
    RETURN NEW;
END $$ LANGUAGE plpgsql;

CREATE TRIGGER trg_review_no_self BEFORE INSERT OR UPDATE ON review
    FOR EACH ROW EXECUTE FUNCTION fn_review_no_self_review();

-- [ERD] pair rule: one script pair = ONE speaker (same take, same session)
CREATE OR REPLACE FUNCTION fn_recording_single_speaker() RETURNS trigger AS $$ BEGIN
    IF EXISTS (SELECT 1 FROM recording r
               WHERE r.script_id = NEW.script_id
                 AND r.speaker_id <> NEW.speaker_id) THEN
        RAISE EXCEPTION 'script % must be recorded by a single speaker', NEW.script_id;
    END IF;
    RETURN NEW;
END $$ LANGUAGE plpgsql;

CREATE TRIGGER trg_recording_single_speaker BEFORE INSERT OR UPDATE ON recording
    FOR EACH ROW EXECUTE FUNCTION fn_recording_single_speaker();

-- [ERD] only APPROVED recordings may join a dataset
CREATE OR REPLACE FUNCTION fn_dataset_recording_approved_only() RETURNS trigger AS $$ DECLARE
    v_status recording_status;
BEGIN
    SELECT status INTO v_status FROM recording WHERE recording_id = NEW.recording_id;
    IF v_status IS DISTINCT FROM 'approved' THEN
        RAISE EXCEPTION 'recording % is not approved (status: %)', NEW.recording_id, v_status;
    END IF;
    RETURN NEW;
END $$ LANGUAGE plpgsql;

CREATE TRIGGER trg_dataset_recording_approved BEFORE INSERT ON dataset_recording
    FOR EACH ROW EXECUTE FUNCTION fn_dataset_recording_approved_only();

-- [ERD] reviewer-detected text error: content-category rejection
--       resets the SCRIPT to pending_validation (cycle-back, like speaker skip)
CREATE OR REPLACE FUNCTION fn_review_content_resets_script() RETURNS trigger AS $$ DECLARE
    v_script_id VARCHAR(11);
    v_decision  review_decision;
    v_category  rejection_category;
BEGIN
    SELECT r.script_id, rv.decision INTO v_script_id, v_decision
    FROM review rv JOIN recording r ON r.recording_id = rv.recording_id
    WHERE rv.review_id = NEW.review_id;

    SELECT category INTO v_category FROM rejection_reason WHERE reason_id = NEW.reason_id;

    IF v_decision = 'rejected' AND v_category = 'content' THEN
        UPDATE script SET status = 'pending_validation'
        WHERE script_id = v_script_id AND status = 'validated';
    END IF;
    RETURN NEW;
END $$ LANGUAGE plpgsql;

CREATE TRIGGER trg_rr_content_resets_script AFTER INSERT ON review_rejection_reason
    FOR EACH ROW EXECUTE FUNCTION fn_review_content_resets_script();

-- [ERD] after 3 independent reviews, final status = majority decision
CREATE OR REPLACE FUNCTION fn_review_majority_status() RETURNS trigger AS $$ DECLARE
    v_total    INT;
    v_approved INT;
    v_final    recording_status;
BEGIN
    SELECT COUNT(*), COUNT(*) FILTER (WHERE decision = 'approved')
    INTO v_total, v_approved
    FROM review WHERE recording_id = NEW.recording_id;

    IF v_total = 3 THEN
        v_final := CASE WHEN v_approved >= 2 THEN 'approved' ELSE 'rejected' END;
        UPDATE recording SET status = v_final
        WHERE recording_id = NEW.recording_id AND status = 'pending_review';
    END IF;
    RETURN NEW;
END $$ LANGUAGE plpgsql;

CREATE TRIGGER trg_review_majority AFTER INSERT ON review
    FOR EACH ROW EXECUTE FUNCTION fn_review_majority_status();

-- ------------------------------------------------------------
-- Generic audit trigger. Usage: attach per table with
--   (entity_name, pk_column_name) args. The acting user is read
--   from the session GUC — set it per transaction from .NET:
--       SET LOCAL app.user_id = '42';
-- 'import'/'export'/'assign'/'release'/'login' actions are
-- logged explicitly by the application.
-- ------------------------------------------------------------
CREATE OR REPLACE FUNCTION fn_audit() RETURNS trigger AS $$ DECLARE
    v_entity TEXT := TG_ARGV[0];
    v_pk     TEXT := TG_ARGV[1];
    v_user   BIGINT := NULLIF(current_setting('app.user_id', true), '')::bigint;
BEGIN
    INSERT INTO audit_log (user_id, entity_type, entity_id, action, old_value, new_value)
    VALUES (
        v_user,
        v_entity,
        CASE WHEN TG_OP = 'DELETE' THEN to_jsonb(OLD) ->> v_pk
             ELSE to_jsonb(NEW) ->> v_pk END,
        (CASE TG_OP WHEN 'INSERT' THEN 'create'
                    WHEN 'UPDATE' THEN 'update'
                    ELSE 'delete' END)::audit_action,
        CASE WHEN TG_OP = 'INSERT' THEN NULL ELSE to_jsonb(OLD) END,
        CASE WHEN TG_OP = 'DELETE' THEN NULL ELSE to_jsonb(NEW) END
    );
    RETURN NULL;   -- AFTER trigger
END $$ LANGUAGE plpgsql;

-- Example attachment — replicate this line for other tables:
CREATE TRIGGER trg_audit_script
    AFTER INSERT OR UPDATE OR DELETE ON script
    FOR EACH ROW EXECUTE FUNCTION fn_audit('script', 'script_id');

-- ============================================================
-- 6. ID GENERATION HELPERS (call from backend/API)
-- ============================================================

-- script_id = s_ + d1(en words) + d2(domain) + d3(relation) + 6-digit serial
-- serial is GLOBAL and sequential ("tăng dần, không random").
CREATE OR REPLACE FUNCTION fn_generate_script_id(
    p_en_word_count INT,            -- 1..9
    p_domain        script_domain,
    p_relation_code INT             -- 1 = direct translation, 2 = proper noun
) RETURNS VARCHAR(11) AS $$ BEGIN
    IF p_en_word_count NOT BETWEEN 1 AND 9 THEN
        RAISE EXCEPTION 'en_word_count must be 1..9 (single ID digit)';
    END IF;
    IF p_relation_code NOT IN (1, 2) THEN
        RAISE EXCEPTION 'relation_code must be 1 (direct translation) or 2 (proper noun)';
    END IF;
    RETURN 's_'
        || p_en_word_count::text
        || (CASE p_domain WHEN 'it_technology' THEN '1'
                          WHEN 'education'     THEN '2'
                          WHEN 'daily_life'    THEN '3' END)
        || p_relation_code::text
        || lpad(nextval('script_serial_seq')::text, 6, '0');
END $$ LANGUAGE plpgsql;

-- recording_id from script_id: r_cs_/r_vi_ + same 9 digits (+ _tN for take N>=2)
CREATE OR REPLACE FUNCTION fn_generate_recording_id(
    p_script_id VARCHAR(11),
    p_variant   sentence_variant,
    p_take      INT DEFAULT 1
) RETURNS VARCHAR(20) AS $$ BEGIN
    IF p_script_id !~ '^s_[0-9]{9}$' THEN
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

-- ============================================================
-- 7. SEED DATA
-- ============================================================
INSERT INTO role (role_name, description) VALUES
    ('speaker',      'Records speech; reviews/flags text before recording'),
    ('reviewer',     'Reviews recordings and text quality'),
    ('task_manager', 'Creates, assigns, and monitors tasks'),
    ('admin',        'Manages data, users, configuration, and statistics');

INSERT INTO script_error_reason (reason_code, description, sort_order) VALUES
    ('meaningless',    'Sentence has no clear meaning', 10),
    ('unnatural',      'Unnatural Vietnamese-English code-switching', 20),
    ('grammar',        'Grammatical error', 30),
    ('spelling',       'Spelling / typo', 40),
    ('mismatch',       'cs / ve sentences are not equivalent', 50),
    ('duplicate',      'Duplicate of an existing script', 60),
    ('other',          'Other text issue', 99);

INSERT INTO rejection_reason (reason_code, category, description) VALUES
    ('content_mismatch',    'content',        'Spoken content does not match the script text'),
    ('script_text_error',   'content',        'Script text itself is wrong (cycles script back to text review)'),
    ('background_noise',    'audio_quality',  'Background noise or interference'),
    ('low_volume',          'audio_quality',  'Volume too low'),
    ('clipping_distortion', 'audio_quality',  'Audio clipping or distortion'),
    ('excess_silence',      'audio_quality',  'Leading/trailing silence too long'),
    ('mispronunciation',    'pronunciation',  'Word mispronounced'),
    ('word_skipped_added',  'pronunciation',  'Words skipped, added, or replaced while reading'),
    ('other',               'other',          'Other issue');

INSERT INTO system_config (config_key, config_value, value_type, description) VALUES
    ('review.rounds_required',              '3',     'int',    'Independent reviews per recording'),
    ('review.default_blind',                'true',  'bool',   'Reviews are blind by default'),
    ('recording.max_leading_silence_sec',   '1',     'int',    'Max silence before speech (speaker guidance)'),
    ('recording.max_trailing_silence_sec',  '1',     'int',    'Max silence after speech (speaker guidance)'),
    ('recording.audio_format_default',      'wav',   'string', 'Default audio format'),
    ('recording.max_take',                  '99',    'int',    'Highest re-record take number'),
    ('import.max_scripts_per_batch',        '100000','int',    'Sanity cap per import batch');

-- Bootstrap admin — REPLACE the hash with a real bcrypt hash before first login.
INSERT INTO app_user (role_id, full_name, email, password_hash, status)
SELECT role_id, 'System Admin', 'admin@codeswitchlabel.local',
       '$2a$11$REPLACE_WITH_REAL_BCRYPT_HASH_BEFORE_FIRST_LOGIN', 'active'
FROM role WHERE role_name = 'admin';

-- ============================================================
-- 8. DASHBOARD / STATISTICS VIEWS  (proposal §3.2c — Admin)
-- ============================================================

CREATE VIEW v_dashboard_summary AS
SELECT
    (SELECT COUNT(*) FROM script)                                    AS total_scripts,
    (SELECT COUNT(*) FROM script WHERE status = 'validated')         AS validated_scripts,
    (SELECT COUNT(*) FROM script WHERE status = 'pending_validation') AS pending_scripts,
    (SELECT COUNT(*) FROM recording)                                 AS total_recordings,
    (SELECT COUNT(*) FROM recording WHERE status = 'approved')       AS approved_recordings,
    (SELECT COUNT(*) FROM recording WHERE status = 'rejected')       AS rejected_recordings,
    (SELECT COALESCE(SUM(duration_sec), 0) FROM recording
        WHERE status = 'approved')                                   AS approved_duration_sec,
    (SELECT COUNT(*) FROM speaker_profile)                           AS speakers,
    (SELECT COUNT(*) FROM app_user
        WHERE role_id = (SELECT role_id FROM role WHERE role_name = 'reviewer')) AS reviewers,
    (SELECT COUNT(*) FROM dataset WHERE status = 'released')         AS released_datasets;

CREATE VIEW v_speaker_performance AS
SELECT
    u.user_id,
    u.full_name,
    COUNT(r.recording_id)                                        AS recordings,
    COUNT(*) FILTER (WHERE r.status = 'approved')                AS approved,
    COUNT(*) FILTER (WHERE r.status = 'rejected')                AS rejected,
    ROUND(100.0 * COUNT(*) FILTER (WHERE r.status = 'approved')
          / NULLIF(COUNT(r.recording_id), 0), 1)                 AS approval_rate_pct
FROM app_user u
JOIN recording r ON r.speaker_id = u.user_id
GROUP BY u.user_id, u.full_name;

CREATE VIEW v_rejection_reason_stats AS
SELECT
    rr.reason_code,
    rr.category,
    COUNT(*) AS times_used
FROM review_rejection_reason rrr
JOIN rejection_reason rr ON rr.reason_id = rrr.reason_id
GROUP BY rr.reason_code, rr.category;

CREATE VIEW v_reviewer_performance AS
SELECT
    u.user_id,
    u.full_name,
    COUNT(rv.review_id)                              AS reviews_done,
    COUNT(*) FILTER (WHERE rv.decision = 'approved') AS approvals,
    COUNT(*) FILTER (WHERE rv.decision = 'rejected') AS rejections
FROM app_user u
JOIN review rv ON rv.reviewer_id = u.user_id
GROUP BY u.user_id, u.full_name;

CREATE VIEW v_task_progress AS
SELECT
    t.task_id,
    t.task_type,
    t.status        AS task_status,
    t.target_qty,
    t.deadline,
    (SELECT COUNT(*) FROM task_script ts WHERE ts.task_id = t.task_id) AS scripts_assigned,
    (SELECT COUNT(*) FROM task_script ts
        WHERE ts.task_id = t.task_id AND ts.status = 'completed')      AS scripts_completed,
    (SELECT COUNT(*) FROM review rv WHERE rv.task_id = t.task_id)      AS reviews_done
FROM task t;

-- Release validation: scripts inside a dataset missing one of the r_cs / r_vi pair
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