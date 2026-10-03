-- ============================================================
-- CodeSwitchLabel — Speech Data Collection & QC System
-- PostgreSQL schema (v11+, 13+ recommended)
-- Run: psql -d codeswitchlabel -f codeswitchlabel_schema.sql
-- ============================================================

BEGIN;

-- ============================================================
-- 1. ENUM TYPES
-- ============================================================
CREATE TYPE user_status           AS ENUM ('active','inactive','suspended');
CREATE TYPE occupation            AS ENUM ('student','employed','other');
CREATE TYPE config_value_type     AS ENUM ('int','bool','string');
CREATE TYPE audit_action          AS ENUM ('create','update','delete','import','export','assign','release','login');
CREATE TYPE script_status         AS ENUM ('pending_validation','validated','rejected','deactivated');
CREATE TYPE script_domain         AS ENUM ('it_technology','education','daily_life');
CREATE TYPE script_review_action  AS ENUM ('accepted','edited','rejected');
CREATE TYPE script_word_relation  AS ENUM ('semantic_equivalent','proper_noun');
CREATE TYPE task_type             AS ENUM ('recording','review');
CREATE TYPE campaign_status         AS ENUM ('draft','open','in_progress','completed','cancelled');
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

-- Domains a reviewer is qualified to validate (Admin cannot judge every domain).
CREATE TABLE user_domain (
    user_id     BIGINT NOT NULL REFERENCES app_user(user_id) ON DELETE CASCADE,
    domain      script_domain NOT NULL,
    assigned_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    PRIMARY KEY (user_id, domain)
);
CREATE INDEX idx_user_domain_domain ON user_domain (domain);

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
    cs_content      TEXT NOT NULL,                 -- code-switching sentence
    vi_content      TEXT NOT NULL,                 -- pure Vietnamese equivalent
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
    --   digit 3 (pos 5) = EN-VN relation: 2 if the script has any proper_noun
    --   row in script_word, else 1. The relation is stored per word in
    --   script_word, so digits 1 and 3 are checked at COMMIT by the deferred
    --   constraint trigger trg_script_word_consistency (section 5).
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

-- One row per EMBEDDED ENGLISH word of script.cs_content, storing both sides
-- (English word + Vietnamese counterpart) and how they relate. Mirrors the
-- "alignment" block of input_text.json. Words without a row are Vietnamese.
CREATE TABLE script_word (
    script_id     VARCHAR(11) NOT NULL REFERENCES script(script_id) ON DELETE CASCADE,
    word_position SMALLINT NOT NULL,               -- 1-based position in cs_content
    en_word       VARCHAR(100) NOT NULL,
    vi_word       VARCHAR(100) NOT NULL,           -- = en_word when proper_noun
    relation      script_word_relation NOT NULL DEFAULT 'semantic_equivalent',
    PRIMARY KEY (script_id, word_position),
    CONSTRAINT ck_script_word_position CHECK (word_position >= 1),
    CONSTRAINT ck_script_word_proper   CHECK (relation <> 'proper_noun' OR vi_word = en_word)
);

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
    edited_vi_content TEXT,
    comment           TEXT,
    reviewed_at       TIMESTAMPTZ NOT NULL DEFAULT now(),
    -- edits always apply to the sentence PAIR as a unit
    CONSTRAINT ck_script_review_edited CHECK (action <> 'edited'
        OR (edited_cs_content IS NOT NULL AND edited_vi_content IS NOT NULL))
);

CREATE INDEX idx_script_review_script ON script_review (script_id);
CREATE INDEX idx_script_review_user   ON script_review (user_id);

-- ------------------------------------------------------------
-- CAMPAIGN / TASK
-- ------------------------------------------------------------
-- A campaign is the planning unit created by the Task Manager for a
-- collection period (typically 1-2 weeks / month).
CREATE TABLE campaign (
    campaign_id   BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    campaign_name VARCHAR(255) NOT NULL,
    target_qty    INT NOT NULL,
    start_date    DATE NOT NULL,
    end_date      DATE NOT NULL,
    status        campaign_status NOT NULL DEFAULT 'draft',
    created_by    BIGINT NOT NULL REFERENCES app_user(user_id),
    assigned_to   BIGINT REFERENCES app_user(user_id) ON DELETE SET NULL,
    created_at    TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT ck_campaign_target_qty CHECK (target_qty BETWEEN 2000 AND 5000),
    CONSTRAINT ck_campaign_dates CHECK (start_date <= end_date)
);

CREATE INDEX idx_campaign_created_by ON campaign (created_by);
CREATE INDEX idx_campaign_status     ON campaign (status);
CREATE INDEX idx_campaign_dates      ON campaign (start_date, end_date);
CREATE INDEX idx_campaign_assigned_to ON campaign (assigned_to);

CREATE TABLE task (
    task_id      BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    campaign_id  BIGINT NOT NULL REFERENCES campaign(campaign_id),
    created_by   BIGINT NOT NULL REFERENCES app_user(user_id),
    task_type    task_type NOT NULL,
    description  TEXT,
    target_qty   INT NOT NULL CHECK (target_qty > 0),
    deadline     TIMESTAMPTZ,
    status       task_status NOT NULL DEFAULT 'draft',
    created_at   TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX idx_task_campaign ON task (campaign_id);

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
    qc_metrics       JSONB,                        -- auto-QC result at upload; NULL until run
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
-- 4. AUDIT_LOG MONTHLY PARTITIONS (2025-01 .. 2027-12)
--    Covers the whole capstone (09/2026 - 03/2027) with room to spare.
--    Extend yearly, or manage with pg_partman / cron.
-- ============================================================
DO $$ DECLARE
    d DATE;
BEGIN
    FOR i IN 0..35 LOOP
        d := DATE '2025-01-01' + (i * INTERVAL '1 month');
        EXECUTE format(
            'CREATE TABLE IF NOT EXISTS audit_log_%s PARTITION OF audit_log
             FOR VALUES FROM (%L) TO (%L)',
            to_char(d, 'YYYY_MM'), d, d + INTERVAL '1 month');
    END LOOP;
END $$;

-- Safety net: if the date ever passes the last monthly partition above
-- (2027-12), audited INSERT/UPDATE on script, campaign and task would fail
-- with "no partition found". Rows landing here are caught instead.
-- Before creating a new monthly partition, move any rows out of the default
-- partition that fall in that month (or use pg_partman).
CREATE TABLE audit_log_default PARTITION OF audit_log DEFAULT;

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

-- [ERD / CAMPAIGN] Campaign creators must be Admin.
-- Task creators must be Task Manager.
CREATE OR REPLACE FUNCTION fn_require_task_manager_creator() RETURNS trigger AS $$
DECLARE
    v_role_name VARCHAR(32);
BEGIN
    SELECT r.role_name
      INTO v_role_name
      FROM app_user u
      JOIN role r ON r.role_id = u.role_id
     WHERE u.user_id = NEW.created_by;

    IF TG_TABLE_NAME = 'campaign' THEN
        IF v_role_name IS DISTINCT FROM 'admin' THEN
            RAISE EXCEPTION 'campaign creator (user %) must have admin role', NEW.created_by;
        END IF;
    ELSIF TG_TABLE_NAME = 'task' THEN
        IF v_role_name IS DISTINCT FROM 'task_manager' THEN
            RAISE EXCEPTION 'task creator (user %) must have task_manager role', NEW.created_by;
        END IF;
    END IF;

    RETURN NEW;
END $$ LANGUAGE plpgsql;

CREATE TRIGGER trg_campaign_creator_role
    BEFORE INSERT OR UPDATE OF created_by ON campaign
    FOR EACH ROW EXECUTE FUNCTION fn_require_task_manager_creator();

CREATE TRIGGER trg_task_creator_role
    BEFORE INSERT OR UPDATE OF created_by ON task
    FOR EACH ROW EXECUTE FUNCTION fn_require_task_manager_creator();

-- [ERD / CAMPAIGN] Every task deadline, when present, must fall
-- inside its campaign period (inclusive of the end date).
CREATE OR REPLACE FUNCTION fn_validate_task_campaign_window() RETURNS trigger AS $$
DECLARE
    v_start_date DATE;
    v_end_date   DATE;
BEGIN
    SELECT start_date, end_date
      INTO v_start_date, v_end_date
      FROM campaign
     WHERE campaign_id = NEW.campaign_id;

    IF NOT FOUND THEN
        RAISE EXCEPTION 'campaign % does not exist', NEW.campaign_id;
    END IF;

    IF NEW.deadline IS NOT NULL
       AND (
           NEW.deadline < v_start_date::timestamptz
           OR NEW.deadline >= (v_end_date + 1)::timestamptz
       ) THEN
        RAISE EXCEPTION
            'task % deadline % must be within campaign % (% to %)',
            NEW.task_id, NEW.deadline, NEW.campaign_id, v_start_date, v_end_date;
    END IF;

    RETURN NEW;
END $$ LANGUAGE plpgsql;

CREATE TRIGGER trg_task_campaign_window
    BEFORE INSERT OR UPDATE OF campaign_id, deadline ON task
    FOR EACH ROW EXECUTE FUNCTION fn_validate_task_campaign_window();

-- [ERD / CAMPAIGN] The sum of task target quantities in one campaign
-- may not exceed the campaign target.
--
-- The campaign row is locked in a deterministic order so concurrent
-- allocations to the same campaign (or moves between campaigns)
-- cannot race or deadlock because of lock-order inversion.
CREATE OR REPLACE FUNCTION fn_validate_campaign_task_target() RETURNS trigger AS $$
DECLARE
    v_new_target       INT;
    v_new_total        BIGINT;
    v_old_target       INT;
    v_first_campaign   BIGINT;
    v_second_campaign  BIGINT;
BEGIN
    IF TG_OP = 'DELETE' THEN
        -- Deleting a task cannot increase allocation.
        RETURN OLD;
    END IF;

    IF TG_OP = 'INSERT' THEN
        SELECT c.target_qty
          INTO v_new_target
          FROM campaign c
         WHERE c.campaign_id = NEW.campaign_id
         FOR UPDATE;

        IF NOT FOUND THEN
            RAISE EXCEPTION 'campaign % does not exist', NEW.campaign_id;
        END IF;

        SELECT COALESCE(SUM(t.target_qty), 0)
          INTO v_new_total
          FROM task t
         WHERE t.campaign_id = NEW.campaign_id;

        IF v_new_total + NEW.target_qty > v_new_target THEN
            RAISE EXCEPTION
                'campaign % target exceeded: allocated task quantity % + task quantity % > campaign target %',
                NEW.campaign_id, v_new_total, NEW.target_qty, v_new_target;
        END IF;

        RETURN NEW;
    END IF;

    IF NEW.campaign_id = OLD.campaign_id THEN
        SELECT c.target_qty
          INTO v_new_target
          FROM campaign c
         WHERE c.campaign_id = NEW.campaign_id
         FOR UPDATE;

        IF NOT FOUND THEN
            RAISE EXCEPTION 'campaign % does not exist', NEW.campaign_id;
        END IF;

        SELECT COALESCE(SUM(t.target_qty), 0)
          INTO v_new_total
          FROM task t
         WHERE t.campaign_id = NEW.campaign_id
           AND t.task_id <> OLD.task_id;

        IF v_new_total + NEW.target_qty > v_new_target THEN
            RAISE EXCEPTION
                'campaign % target exceeded: allocated task quantity % + task quantity % > campaign target %',
                NEW.campaign_id, v_new_total, NEW.target_qty, v_new_target;
        END IF;

        RETURN NEW;
    END IF;

    -- UPDATE moving a task from one campaign to another.
    -- Lock both campaign rows in ascending campaign_id order.
    v_first_campaign := LEAST(OLD.campaign_id, NEW.campaign_id);
    v_second_campaign := GREATEST(OLD.campaign_id, NEW.campaign_id);

    PERFORM 1
      FROM campaign
     WHERE campaign_id = v_first_campaign
     FOR UPDATE;

    PERFORM 1
      FROM campaign
     WHERE campaign_id = v_second_campaign
     FOR UPDATE;

    SELECT c.target_qty
      INTO v_old_target
      FROM campaign c
     WHERE c.campaign_id = OLD.campaign_id;

    SELECT c.target_qty
      INTO v_new_target
      FROM campaign c
     WHERE c.campaign_id = NEW.campaign_id;

    IF v_old_target IS NULL OR v_new_target IS NULL THEN
        RAISE EXCEPTION 'source or destination campaign does not exist';
    END IF;

    -- The old campaign cannot become over-allocated by moving a task out.
    -- Check only the destination campaign, excluding the row being moved.
    SELECT COALESCE(SUM(t.target_qty), 0)
      INTO v_new_total
      FROM task t
     WHERE t.campaign_id = NEW.campaign_id
       AND t.task_id <> OLD.task_id;

    IF v_new_total + NEW.target_qty > v_new_target THEN
        RAISE EXCEPTION
            'campaign % target exceeded: allocated task quantity % + moved task quantity % > campaign target %',
            NEW.campaign_id, v_new_total, NEW.target_qty, v_new_target;
    END IF;

    RETURN NEW;
END $$ LANGUAGE plpgsql;

CREATE TRIGGER trg_task_campaign_target
    BEFORE INSERT OR UPDATE OF campaign_id, target_qty ON task
    FOR EACH ROW EXECUTE FUNCTION fn_validate_campaign_task_target();

-- [ERD / CAMPAIGN] Do not lower a campaign target below the
-- quantities already allocated to its tasks.
CREATE OR REPLACE FUNCTION fn_validate_campaign_target_update() RETURNS trigger AS $$
DECLARE
    v_task_total BIGINT;
BEGIN
    IF NEW.target_qty IS DISTINCT FROM OLD.target_qty THEN
        SELECT COALESCE(SUM(target_qty), 0)
          INTO v_task_total
          FROM task
         WHERE campaign_id = NEW.campaign_id;

        IF v_task_total > NEW.target_qty THEN
            RAISE EXCEPTION
                'campaign % target % is below allocated task quantity %',
                NEW.campaign_id, NEW.target_qty, v_task_total;
        END IF;
    END IF;

    IF NEW.start_date > NEW.end_date THEN
        RAISE EXCEPTION 'campaign % start_date must be on or before end_date',
            NEW.campaign_id;
    END IF;

    -- Existing task deadlines must remain inside the new campaign window.
    IF NEW.start_date IS DISTINCT FROM OLD.start_date
       OR NEW.end_date IS DISTINCT FROM OLD.end_date THEN
        IF EXISTS (
            SELECT 1
              FROM task
             WHERE campaign_id = NEW.campaign_id
               AND deadline IS NOT NULL
               AND (
                   deadline < NEW.start_date::timestamptz
                   OR deadline >= (NEW.end_date + 1)::timestamptz
               )
        ) THEN
            RAISE EXCEPTION
                'campaign % date range would make one or more task deadlines invalid',
                NEW.campaign_id;
        END IF;
    END IF;

    RETURN NEW;
END $$ LANGUAGE plpgsql;

CREATE TRIGGER trg_campaign_target_update
    BEFORE UPDATE OF target_qty, start_date, end_date ON campaign
    FOR EACH ROW EXECUTE FUNCTION fn_validate_campaign_target_update();

-- [ERD / CAMPAIGN] assigned_to must be a Task Manager (or NULL).
CREATE OR REPLACE FUNCTION fn_validate_campaign_assigned_to() RETURNS trigger AS $$
DECLARE
    v_role_name VARCHAR(32);
BEGIN
    IF NEW.assigned_to IS NOT NULL THEN
        SELECT r.role_name INTO v_role_name
        FROM app_user u JOIN role r ON r.role_id = u.role_id
        WHERE u.user_id = NEW.assigned_to;

        IF v_role_name IS DISTINCT FROM 'task_manager' THEN
            RAISE EXCEPTION 'assigned_to user % must have task_manager role', NEW.assigned_to;
        END IF;
    END IF;
    RETURN NEW;
END $$ LANGUAGE plpgsql;

CREATE TRIGGER trg_campaign_assigned_to_role
    BEFORE INSERT OR UPDATE OF assigned_to ON campaign
    FOR EACH ROW EXECUTE FUNCTION fn_validate_campaign_assigned_to();

-- [ERD / USER_DOMAIN] A script may only become 'validated' after an
-- accepted/edited SCRIPT_REVIEW by a user qualified for the script's domain.
CREATE OR REPLACE FUNCTION fn_script_validated_by_domain_reviewer() RETURNS trigger AS $$ BEGIN
    IF NEW.status = 'validated'
       AND (TG_OP = 'INSERT' OR OLD.status IS DISTINCT FROM 'validated') THEN
        IF NOT EXISTS (
            SELECT 1
              FROM script_review sr
              JOIN user_domain ud ON ud.user_id = sr.user_id
                                 AND ud.domain  = NEW.domain
             WHERE sr.script_id = NEW.script_id
               AND sr.action IN ('accepted','edited')
        ) THEN
            RAISE EXCEPTION
                'script % cannot be validated: no accepted/edited review by a reviewer qualified for domain %',
                NEW.script_id, NEW.domain;
        END IF;
    END IF;
    RETURN NEW;
END $$ LANGUAGE plpgsql;

CREATE TRIGGER trg_script_validated_domain
    BEFORE INSERT OR UPDATE OF status ON script
    FOR EACH ROW EXECUTE FUNCTION fn_script_validated_by_domain_reviewer();

-- [ERD / SCRIPT_WORD] script_id digits must agree with script_word:
--   digit 1 = number of English words = COUNT(script_word rows)
--   digit 3 = 2 if any word is proper_noun, else 1
-- Deferred to COMMIT so a script and its words can be inserted in any order
-- inside one transaction (import / edit-regenerate).
CREATE OR REPLACE FUNCTION fn_script_word_consistency() RETURNS trigger AS $$
DECLARE
    v_script_id VARCHAR(11);
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
        RETURN NULL;   -- script was deleted (words cascade); nothing to check
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

CREATE CONSTRAINT TRIGGER trg_script_word_consistency
    AFTER INSERT OR UPDATE OR DELETE ON script_word
    DEFERRABLE INITIALLY DEFERRED
    FOR EACH ROW EXECUTE FUNCTION fn_script_word_consistency();

CREATE CONSTRAINT TRIGGER trg_script_consistency
    AFTER INSERT OR UPDATE OF en_word_count ON script
    DEFERRABLE INITIALLY DEFERRED
    FOR EACH ROW EXECUTE FUNCTION fn_script_word_consistency();

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

CREATE TRIGGER trg_audit_campaign
    AFTER INSERT OR UPDATE OR DELETE ON campaign
    FOR EACH ROW EXECUTE FUNCTION fn_audit('campaign', 'campaign_id');

CREATE TRIGGER trg_audit_task
    AFTER INSERT OR UPDATE OR DELETE ON task
    FOR EACH ROW EXECUTE FUNCTION fn_audit('task', 'task_id');

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
    ('import.max_scripts_per_batch',        '100000','int',    'Sanity cap per import batch'),
    -- anti-junk thresholds: PLACEHOLDER values, confirm with supervisor
    ('quality.speaker_min_approval_rate',        '60', 'int', 'Min approval rate (%) before a speaker is flagged'),
    ('quality.speaker_max_consecutive_qc_fail',  '5',  'int', 'Consecutive qc_failed recordings before warning/suspension'),
    ('quality.min_recordings_before_eval',       '20', 'int', 'Recordings needed before approval rate is evaluated');

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
    (SELECT COUNT(*) FROM campaign)                                  AS total_campaigns,
    (SELECT COUNT(*) FROM campaign WHERE status = 'open')             AS open_campaigns,
    (SELECT COUNT(*) FROM campaign WHERE status = 'in_progress')     AS active_campaigns,
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
    t.campaign_id,
    c.campaign_name,
    t.task_type,
    t.status        AS task_status,
    t.target_qty,
    t.deadline,
    (SELECT COUNT(*) FROM task_script ts WHERE ts.task_id = t.task_id) AS scripts_assigned,
    (SELECT COUNT(*) FROM task_script ts
        WHERE ts.task_id = t.task_id AND ts.status = 'completed')      AS scripts_completed,
    (SELECT COUNT(*) FROM review rv WHERE rv.task_id = t.task_id)      AS reviews_done
FROM task t
JOIN campaign c ON c.campaign_id = t.campaign_id;

-- Campaign-level planning/progress summary
CREATE VIEW v_campaign_progress AS
SELECT
    c.campaign_id,
    c.campaign_name,
    c.target_qty AS campaign_target_qty,
    c.start_date,
    c.end_date,
    c.status AS campaign_status,
    c.assigned_to,
    COALESCE(SUM(t.target_qty), 0) AS allocated_task_qty,
    c.target_qty - COALESCE(SUM(t.target_qty), 0) AS remaining_task_qty,
    COUNT(t.task_id) AS task_count,
    COUNT(t.task_id) FILTER (WHERE t.status = 'completed') AS completed_task_count
FROM campaign c
LEFT JOIN task t ON t.campaign_id = c.campaign_id
GROUP BY
    c.campaign_id,
    c.campaign_name,
    c.target_qty,
    c.start_date,
    c.end_date,
    c.status,
    c.assigned_to;

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