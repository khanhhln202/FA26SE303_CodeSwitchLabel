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
-- TEAM_013: task còn ba trạng thái. Đợt còn ba trạng thái và là HÀM của task, không bấm tay.
CREATE TYPE campaign_status         AS ENUM ('open','in_progress','completed');
CREATE TYPE task_status           AS ENUM ('in_progress','completed','cancelled');
CREATE TYPE assignment_status     AS ENUM ('active','completed','reassigned','cancelled');
CREATE TYPE task_script_status    AS ENUM ('pending','completed','rejected');
CREATE TYPE sentence_variant      AS ENUM ('code_switching','pure_vietnamese');
CREATE TYPE recording_status      AS ENUM ('qc_failed','pending_review','approved','rejected');
CREATE TYPE task_recording_status AS ENUM ('queued','reviewed','skipped');
CREATE TYPE review_decision       AS ENUM ('approved','rejected');
CREATE TYPE rejection_category    AS ENUM ('content','audio_quality','pronunciation','other');

-- TEAM_010: Một hàng đợi chung cho MỌI câu văn bản (yêu cầu 2.1). Mỗi câu đi qua
-- hai chặng theo thứ tự: text_review -> recording. enqueued_at là mốc FIFO.
CREATE TYPE queue_stage           AS ENUM ('text_review','recording');
CREATE TYPE queue_state           AS ENUM ('queued','claimed','done','dropped');
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
CREATE UNIQUE INDEX uq_script_cs_content_md5 ON script (md5(cs_content));

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
        OR (edited_cs_content IS NOT NULL AND edited_vi_content IS NOT NULL)),
    CONSTRAINT ck_script_review_rejection_reason CHECK (action <> 'rejected'
        OR error_reason_id IS NOT NULL)
);

CREATE INDEX idx_script_review_script ON script_review (script_id);
CREATE INDEX idx_script_review_user   ON script_review (user_id);


-- ------------------------------------------------------------
-- CAMPAIGN / TASK
-- ------------------------------------------------------------
-- A campaign is the planning unit created by the Task Manager for a
-- collection period (typically 1-2 weeks / month).
-- TEAM_003: domain = chủ đề của cả đợt. Một đợt chỉ đi trên MỘT chủ đề;
-- trg_task_campaign_domain chặn gắn task khác chủ đề vào đợt này.
CREATE TABLE campaign (
    campaign_id   BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    campaign_name VARCHAR(255) NOT NULL,
    domain        script_domain NOT NULL,
    target_qty    INT NOT NULL,
    start_date    DATE NOT NULL,
    end_date      DATE NOT NULL,
    status        campaign_status NOT NULL DEFAULT 'open',
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
CREATE INDEX idx_campaign_domain     ON campaign (domain);

-- Speaker đăng ký tham gia một chiến dịch ("Đợt" ở FE) — không giới hạn số người.
CREATE TABLE campaign_registration (
    campaign_id   BIGINT NOT NULL REFERENCES campaign(campaign_id) ON DELETE CASCADE,
    speaker_id    BIGINT NOT NULL REFERENCES app_user(user_id) ON DELETE CASCADE,
    registered_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    PRIMARY KEY (campaign_id, speaker_id)
);
CREATE INDEX idx_campaign_registration_speaker ON campaign_registration (speaker_id);

-- campaign_id is NULL for standalone tasks (created outside any campaign,
-- attached to a campaign later, or detached back). Deleting a campaign
-- keeps its tasks: FK is ON DELETE SET NULL; use the campaign-scoped
-- detach endpoint to remove a task from a campaign without deleting it.
-- TEAM_003: domain = chủ đề của nhiệm vụ. Task độc lập thì tự do; task gắn
-- vào đợt thì phải trùng chủ đề với đợt (trg_task_campaign_domain).
CREATE TABLE task (
    task_id      BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    campaign_id  BIGINT REFERENCES campaign(campaign_id) ON DELETE SET NULL,
    created_by   BIGINT NOT NULL REFERENCES app_user(user_id),
    task_type    task_type NOT NULL,
    domain       script_domain NOT NULL,
    description  TEXT,
    target_qty   INT NOT NULL CHECK (target_qty > 0),
    deadline     TIMESTAMPTZ,
    status       task_status NOT NULL DEFAULT 'in_progress',
    created_at   TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX idx_task_campaign ON task (campaign_id);
CREATE INDEX idx_task_domain   ON task (domain);

CREATE TABLE task_assignment (
    assignment_id     BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,  -- surrogate: keeps history
    task_id           BIGINT NOT NULL REFERENCES task(task_id) ON DELETE CASCADE,
    user_id           BIGINT NOT NULL REFERENCES app_user(user_id),
    assigned_at       TIMESTAMPTZ NOT NULL DEFAULT now(),
    assignment_status assignment_status NOT NULL DEFAULT 'active'
);

-- TEAM_010: Một task giao được cho NHIỀU người cùng lúc. Chỉ tiêu target_qty là của
-- CẢ NHÓM (không nhân theo số người), nên quota đợt vẫn cộng đúng một lần.
-- Ràng buộc còn lại: cùng một người không được có hai lượt giao active trên một task.
CREATE UNIQUE INDEX uq_task_assignment_active_user
    ON task_assignment (task_id, user_id) WHERE assignment_status = 'active';
CREATE INDEX idx_task_assignment_user ON task_assignment (user_id);

CREATE TABLE task_script (
    task_id     BIGINT NOT NULL REFERENCES task(task_id) ON DELETE CASCADE,
    script_id   VARCHAR(11) NOT NULL REFERENCES script(script_id),
    status      task_script_status NOT NULL DEFAULT 'pending',
    included_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    PRIMARY KEY (task_id, script_id)
);
CREATE INDEX idx_task_script_script ON task_script (script_id);

-- TEAM_010 — HÀNG ĐỢI CHUNG cho mọi câu văn bản (yêu cầu 2.1).
--
-- Một câu đi qua hai chặng theo thứ tự:
--   1. text_review — chờ người duyệt nội dung (1 lượt). Admin nhập file, người dùng
--      đóng góp và báo lỗi đều đi vào đây.
--   2. recording   — đã duyệt nội dung, chờ người ghi âm (2 bản cs + vi).
--
-- Hàng được sinh bởi TRIGGER, không phải bởi code: script vừa INSERT hoặc chuyển
-- sang pending_validation thì tự có mặt trong hàng đợi. Đó là điều kiện để không
-- câu nào lọt qua được việc duyệt (giống cách trạng thái bản ghi do trigger chốt).
--
-- enqueued_at là mốc FIFO. claim là hàm fn_queue_claim_next dùng FOR UPDATE SKIP
-- LOCKED, nên hai người KHÔNG THỂ nhận cùng một câu (yêu cầu 3.2). claim có hạn
-- (claim_expires_at) để người mở rồi bỏ ngang không giữ câu vĩnh viễn.
CREATE TABLE script_queue (
    queue_id         BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    script_id        VARCHAR(11) NOT NULL REFERENCES script(script_id) ON DELETE CASCADE,
    stage            queue_stage NOT NULL,

    -- Mốc vào hàng đợi = thứ tự FIFO. Tách khỏi script.created_at vì một câu có thể
    -- quay lại hàng đợi nhiều lần (audio bị từ chối), mỗi lần là một mốc mới.
    enqueued_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
    state            queue_state NOT NULL DEFAULT 'queued',

    -- Người đang giữ câu. NULL = chưa ai nhận. Hết hạn thì ai cũng claim lại được.
    claimed_by       BIGINT REFERENCES app_user(user_id) ON DELETE SET NULL,
    claimed_at       TIMESTAMPTZ,
    claim_expires_at TIMESTAMPTZ,

    -- Task duyệt văn bản đã giao câu này cho người đó (yêu cầu 2.2: chia lô 100 câu/người).
    -- NULL = câu đang nằm trong hàng đợi chung, ai cũng lấy được.
    task_id          BIGINT REFERENCES task(task_id) ON DELETE CASCADE,

    CONSTRAINT ck_script_queue_claim CHECK (
        (state = 'claimed' AND claimed_by IS NOT NULL AND claim_expires_at IS NOT NULL) OR
        state <> 'claimed')
);

-- FIFO: chỉ đọc hàng đang mở (queued/claimed), xếp theo enqueued_at.
CREATE INDEX idx_script_queue_pick
    ON script_queue (stage, state, enqueued_at) WHERE state IN ('queued','claimed');
-- Truy tìm nhanh "câu này đang ở chặng nào".
CREATE INDEX idx_script_queue_script ON script_queue (script_id, stage);
-- Hết hạn: index phục vụ việc quét lại hàng đã claim quá hạn.
CREATE INDEX idx_script_queue_claim_expiry
    ON script_queue (claim_expires_at) WHERE state = 'claimed';
CREATE INDEX idx_script_queue_task ON script_queue (task_id) WHERE task_id IS NOT NULL;

-- Một câu chỉ được có MỘT hàng đang mở trong mỗi chặng. Câu bị bỏ (state='dropped')
-- hay đã xong (state='done') thì được tạo hàng mới ở lần sau.
CREATE UNIQUE INDEX uq_script_queue_open
    ON script_queue (script_id, stage) WHERE state IN ('queued','claimed');

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

    -- TEAM_010 (yêu cầu 2.4): dấu vết của nút "Chỉ xóa audio" và "Xóa hoàn toàn".
    --
    -- CỐ Ý KHÔNG ghi thành một hàng `review`: hai hành động này là quyết định dứt điểm
    -- của người duyệt, KHÔNG phải một phiếu bầu. Ghi vào review sẽ làm lệch bộ đếm
    -- 3 lượt và kích hoạt trg_review_majority — có thể kéo câu về hàng đợi duyệt nội
    -- dung một cách không mong muốn.
    discarded_at     TIMESTAMPTZ,
    discarded_by     BIGINT REFERENCES app_user(user_id),
    discard_note     TEXT,

    recorded_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT ck_recording_id_format CHECK
        (recording_id ~ '^r_(cs|vi)_[0-9]{9}(_t([2-9]|[1-9][0-9]+))?$'),
    -- sentence_variant must mirror the prefix
    CONSTRAINT ck_recording_variant_prefix CHECK (
        (sentence_variant = 'code_switching'   AND recording_id ~ '^r_cs_') OR
        (sentence_variant = 'pure_vietnamese' AND recording_id ~ '^r_vi_')),
    -- the 9-digit serial inside recording_id must equal the script's serial
    CONSTRAINT ck_recording_script_serial CHECK
        (regexp_replace(recording_id, '^r_(cs|vi)_([0-9]{9})(_t([2-9]|[1-9][0-9]+))?$', '\2')
         = substr(script_id, 3))
);

CREATE INDEX idx_recording_script  ON recording (script_id);
CREATE INDEX idx_recording_speaker ON recording (speaker_id);
CREATE INDEX idx_recording_task    ON recording (task_id);
CREATE INDEX idx_recording_status  ON recording (status);

-- at most ONE active (pending_review/approved) recording per (script, variant):
-- blocks concurrent retakes racing HasActiveRecording + CountTakes checks.
CREATE UNIQUE INDEX uq_recording_active_variant
    ON recording (script_id, sentence_variant) WHERE status IN ('pending_review', 'approved');

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
    review_round SMALLINT NOT NULL CHECK (review_round >= 1),
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

-- [ERD / CAMPAIGN] Campaign creators must be Admin or Task Manager.
-- Task creators must be Task Manager.
-- TEAM_003: Task Manager tự lập kế hoạch cho đợt của mình nên cũng được
-- tạo chiến dịch. Việc gán đợt cho ai vẫn do trg_task_creator_assigned
-- kiểm tra tầng dưới (task trong đợt do đúng người được giao đợt tạo).
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
        IF v_role_name IS DISTINCT FROM 'admin'
           AND v_role_name IS DISTINCT FROM 'task_manager' THEN
            RAISE EXCEPTION 'campaign creator (user %) must have admin or task_manager role', NEW.created_by;
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
    -- Standalone task (NULL campaign_id): no campaign window to check.
    IF NEW.campaign_id IS NULL THEN
        RETURN NEW;
    END IF;

    SELECT start_date, end_date
      INTO v_start_date, v_end_date
      FROM campaign
     WHERE campaign_id = NEW.campaign_id;

    IF NOT FOUND THEN
        RAISE EXCEPTION 'campaign % does not exist', NEW.campaign_id;
    END IF;

    IF NEW.deadline IS NOT NULL
       AND (
           NEW.deadline < (v_start_date::timestamp AT TIME ZONE 'Asia/Ho_Chi_Minh')
           OR NEW.deadline >= ((v_end_date + 1)::timestamp AT TIME ZONE 'Asia/Ho_Chi_Minh')
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

    -- Detach: leaving a campaign cannot over-allocate anything.
    IF NEW.campaign_id IS NULL THEN
        RETURN NEW;
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

    -- Attach: OLD.campaign_id IS NULL, NEW.campaign_id IS NOT NULL.
    -- Same checks as INSERT (lock new campaign, count its tasks).
    IF OLD.campaign_id IS NULL THEN
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

    -- UPDATE moving a task from one campaign to another (both non-NULL here).
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

-- [TEAM_001 / ERD / TASK] A Task Manager creates tasks only inside campaigns the
-- Admin assigned to them; a campaign with assigned_to NULL accepts no tasks yet.
-- The service layer returns a friendly 422 first; this trigger is the safety net.
CREATE OR REPLACE FUNCTION fn_validate_task_creator_assigned() RETURNS trigger AS $$
DECLARE
    v_assigned_to BIGINT;
BEGIN
    -- Standalone task: no campaign assignment rule applies.
    IF NEW.campaign_id IS NULL THEN
        RETURN NEW;
    END IF;

    SELECT c.assigned_to
      INTO v_assigned_to
      FROM campaign c
     WHERE c.campaign_id = NEW.campaign_id;

    IF NOT FOUND THEN
        RAISE EXCEPTION 'campaign % does not exist', NEW.campaign_id;
    END IF;

    IF v_assigned_to IS NULL THEN
        RAISE EXCEPTION
            'campaign % has no assigned task manager yet; assign it before creating tasks',
            NEW.campaign_id;
    END IF;

    IF v_assigned_to IS DISTINCT FROM NEW.created_by THEN
        RAISE EXCEPTION
            'task creator (user %) must be the assigned task manager (user %) of campaign %',
            NEW.created_by, v_assigned_to, NEW.campaign_id;
    END IF;

    RETURN NEW;
END $$ LANGUAGE plpgsql;

CREATE TRIGGER trg_task_creator_assigned
    BEFORE INSERT OR UPDATE OF campaign_id, created_by ON task
    FOR EACH ROW EXECUTE FUNCTION fn_validate_task_creator_assigned();

-- [TEAM_003 / CAMPAIGN] A task may only join a campaign of the SAME domain.
-- Một đợt đi trên một chủ đề, nên không gắn được nhiệm vụ chủ đề khác vào —
-- nếu không, báo cáo cuối đợt sẽ trộn nhiều chủ đề và số liệu sai nghĩa.
-- Standalone task (campaign_id NULL) has no campaign to compare against.
-- The service layer returns a friendly 422 first; this trigger is the safety net.
CREATE OR REPLACE FUNCTION fn_validate_task_campaign_domain() RETURNS trigger AS $$
DECLARE
    v_campaign_domain script_domain;
BEGIN
    -- Detached task: any domain is fine.
    IF NEW.campaign_id IS NULL THEN
        RETURN NEW;
    END IF;

    SELECT c.domain
      INTO v_campaign_domain
      FROM campaign c
     WHERE c.campaign_id = NEW.campaign_id;

    IF NOT FOUND THEN
        RAISE EXCEPTION 'campaign % does not exist', NEW.campaign_id;
    END IF;

    IF NEW.domain IS DISTINCT FROM v_campaign_domain THEN
        RAISE EXCEPTION
            'task % domain % must match campaign % domain %',
            NEW.task_id, NEW.domain, NEW.campaign_id, v_campaign_domain;
    END IF;

    RETURN NEW;
END $$ LANGUAGE plpgsql;

CREATE TRIGGER trg_task_campaign_domain
    BEFORE INSERT OR UPDATE OF campaign_id, domain ON task
    FOR EACH ROW EXECUTE FUNCTION fn_validate_task_campaign_domain();

-- [TEAM_003 / CAMPAIGN] Changing a campaign's domain must not orphan its tasks:
-- all attached tasks have to follow, otherwise the campaign would keep tasks of
-- a different domain (and trg_task_campaign_domain would then block the update
-- with a message about the wrong task). Reject instead of silently detaching.
CREATE OR REPLACE FUNCTION fn_validate_campaign_domain_change() RETURNS trigger AS $$
DECLARE
    v_mismatched INT;
BEGIN
    IF NEW.domain IS NOT DISTINCT FROM OLD.domain THEN
        RETURN NEW;
    END IF;

    SELECT COUNT(*) INTO v_mismatched
      FROM task t
     WHERE t.campaign_id = NEW.campaign_id
       AND t.domain IS DISTINCT FROM NEW.domain;

    IF v_mismatched > 0 THEN
        RAISE EXCEPTION
            'campaign % still has % task(s) of another domain; move or detach them before changing its domain',
            NEW.campaign_id, v_mismatched;
    END IF;

    RETURN NEW;
END $$ LANGUAGE plpgsql;

CREATE TRIGGER trg_campaign_domain_change
    BEFORE UPDATE OF domain ON campaign
    FOR EACH ROW EXECUTE FUNCTION fn_validate_campaign_domain_change();

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
                   deadline < (NEW.start_date::timestamp AT TIME ZONE 'Asia/Ho_Chi_Minh')
                   OR deadline >= ((NEW.end_date + 1)::timestamp AT TIME ZONE 'Asia/Ho_Chi_Minh')
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
               AND (TG_OP = 'INSERT' OR sr.reviewed_at >= OLD.updated_at)
        ) THEN
            RAISE EXCEPTION
                'script % cannot be validated: no accepted/edited review by a reviewer qualified for domain % after last reset',
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

-- ============================================================
-- TEAM_010 — HÀNG ĐỢI CHUNG (yêu cầu 2.1, 2.2, 3.1, 3.2)
-- ============================================================

-- Hàng đợi do TRIGGER quản lý, không phải code. Lý do: câu vừa được Admin nhập hàng
-- loạt hay người dùng vừa đóng góp phải CÓ mặt trong hàng đợi ngay, kể cả khi
-- đường ghi đó là SQL thẳng. Nếu để service tự thêm hàng thì mọi đường ghi bỏ qua
-- service đều tạo câu không ai duyệt.
CREATE OR REPLACE FUNCTION fn_script_queue_sync() RETURNS trigger AS $$ BEGIN
    IF NEW.status = 'pending_validation' THEN
        -- Vào chặng duyệt nội dung. Dùng 'queued' chứ không claim có chủ: ai đó sẽ
        -- tự lấy sau. ON CONFLICT DO NOTHING vì câu có thể đã có hàng đang mở.
        INSERT INTO script_queue (script_id, stage, state)
        VALUES (NEW.script_id, 'text_review', 'queued')
        ON CONFLICT (script_id, stage) WHERE state IN ('queued','claimed')
        DO NOTHING;

    ELSIF NEW.status = 'validated' THEN
        -- Đã qua duyệt nội dung: đóng hàng chặng 1, mở chặng ghi âm.
        UPDATE script_queue
           SET state = 'dropped'
         WHERE script_id = NEW.script_id AND stage = 'text_review' AND state IN ('queued','claimed');

        INSERT INTO script_queue (script_id, stage, state)
        VALUES (NEW.script_id, 'recording', 'queued')
        ON CONFLICT (script_id, stage) WHERE state IN ('queued','claimed')
        DO NOTHING;

    ELSIF NEW.status IN ('rejected', 'deactivated') THEN
        -- Câu hỏng hoặc đã xoá: rút khỏi mọi chặng, không ai nhận nữa.
        UPDATE script_queue
           SET state = 'dropped'
         WHERE script_id = NEW.script_id AND state IN ('queued','claimed');
    END IF;

    RETURN NEW;
END $$ LANGUAGE plpgsql;

CREATE TRIGGER trg_script_queue_sync
    AFTER INSERT OR UPDATE OF status ON script
    FOR EACH ROW EXECUTE FUNCTION fn_script_queue_sync();

-- Claim một câu cho một người, NGAY TRONG LÀ 1 TRANSACTION.
--
-- SKIP LOCKED là mấu chốt của yêu cầu 3.2: khi hai người gọi cùng lúc, transaction
-- thứ nhất khoá hàng, thứ hai BỎ QUA hàng đó và lấy câu khác — thay vì đứng chờ
-- rồi nhận trùng. Không có SKIP LOCKED thì "không cho 2 người cùng load 1 câu"
-- chỉ có thể đảm bảo ở lúc upload, mà đến lúc đó đã quá muộn cho người đã đọc.
--
-- Hết hạn: claim của người bỏ ngang được nhặt lại sau claim_ttl phút, nếu không
-- thì một câu có thể bị giữ vĩnh viễn chỉ vì ai đó bấm nhầm rồi đóng tab.
CREATE OR REPLACE FUNCTION fn_queue_claim_next(
    p_user_id   BIGINT,
    p_stage     queue_stage,
    p_task_id   BIGINT DEFAULT NULL,
    p_ttl_minutes INT DEFAULT NULL
) RETURNS TABLE (
    script_id     VARCHAR,
    queue_id      BIGINT,
    enqueued_at   TIMESTAMPTZ,
    claim_expires_at TIMESTAMPTZ
) AS $$
DECLARE
    v_ttl    INT;
    v_row    script_queue%ROWTYPE;
BEGIN
    SELECT COALESCE(NULLIF(config_value, '')::int, 30)
      INTO v_ttl
      FROM system_config WHERE config_key = 'queue.claim_ttl_minutes';
    IF v_ttl IS NULL OR v_ttl < 1 THEN
        v_ttl := 30;
    END IF;
    v_ttl := COALESCE(p_ttl_minutes, v_ttl);

    SELECT q.* INTO v_row
      FROM script_queue q
      JOIN script s ON s.script_id = q.script_id
     WHERE q.stage = p_stage
       AND s.status = CASE q.stage
                         WHEN 'text_review' THEN 'pending_validation'::script_status
                         ELSE 'validated'::script_status
                       END
       -- Chưa ai giữ, hoặc đã giác người và hết hạn.
       AND (q.state = 'queued' OR (q.state = 'claimed' AND q.claim_expires_at <= now()))
       -- Chặng nào cũng phải tôn trọng p_task_id, nhưng cách "thuộc task" khác nhau:
       --   duyệt văn bản -> task gắn thẳng vào hàng (q.task_id), do lô của Task Manager.
       --   ghi âm       -> task gắn qua bảng trung gian task_script, vì một câu có thể
       --                   nằm trong nhiều task cùng lúc.
       -- Bỏ điều kiện này thì p_task_id trở thành vô nghĩa: mọi hàng đều task_id NULL
       -- ở chặng ghi âm nên câu ngoài task cũng lọt vào.
       AND (
            p_task_id IS NULL
            OR CASE q.stage
                 WHEN 'text_review' THEN (
                     q.task_id IS NULL
                     OR q.task_id = p_task_id
                     OR EXISTS (SELECT 1 FROM task_assignment a
                                 WHERE a.task_id = q.task_id
                                   AND a.user_id = p_user_id
                                   AND a.assignment_status = 'active')
                 )
                 ELSE EXISTS (SELECT 1 FROM task_script ts
                               WHERE ts.script_id = q.script_id
                                 AND ts.task_id = p_task_id
                                 AND ts.status = 'pending')
               END
           )
     ORDER BY q.enqueued_at, q.queue_id
     FOR UPDATE OF q SKIP LOCKED
     LIMIT 1;

    IF NOT FOUND THEN
        RETURN;
    END IF;

    UPDATE script_queue
       SET state = 'claimed',
           claimed_by = p_user_id,
           claimed_at = now(),
           claim_expires_at = now() + make_interval(mins => v_ttl)
     WHERE script_queue.queue_id = v_row.queue_id;

    RETURN QUERY
    SELECT v_row.script_id, v_row.queue_id, v_row.enqueued_at,
           now() + make_interval(mins => v_ttl);
END $$ LANGUAGE plpgsql;

-- Claim cả lô cho một task duyệt văn bản (yêu cầu 2.2: 100 câu/người).
-- Gán task_id ngay khi claim để câu đó gắn với đúng người được giao.
CREATE OR REPLACE FUNCTION fn_queue_claim_batch(
    p_task_id    BIGINT,
    p_user_id    BIGINT,
    p_count      INT,
    p_stage      queue_stage DEFAULT 'text_review'
) RETURNS SETOF VARCHAR AS $$
DECLARE
    v_claimed VARCHAR;
BEGIN
    FOR i IN 1..p_count LOOP
        SELECT c.script_id INTO v_claimed
          FROM fn_queue_claim_next(p_user_id, p_stage, p_task_id) c;

        EXIT WHEN v_claimed IS NULL;

        UPDATE script_queue
           SET task_id = p_task_id
         WHERE script_id = v_claimed AND stage = p_stage AND claimed_by = p_user_id;

        RETURN NEXT v_claimed;
    END LOOP;
END $$ LANGUAGE plpgsql;

-- Đóng claim: câu đã xong việc (done) hoặc trả lại hàng đợi (queued).
--
-- p_from cho biết được phép chuyển TỪ trạng thái nào. Không có nó thì chỉ chuyển
-- được từ 'claimed', và bản ghi bị từ chối sẽ không quay lại được hàng đợi — vì hàng
-- của nó đã ở 'done' từ lúc câu được thu đủ.
--
-- p_requeue_at_end đẩy câu xuống CUỐI hàng đợi (đặt lại mốc FIFO) thay vì giữ nguyên
-- chỗ cũ. Cần cho mọi trường hợp "lấy được nhưng không dùng được": nếu trả về chỗ
-- cũ thì câu đó vẫn đứng đầu hàng đợi và bị claim lại ngay lập tức, mãi mãi.
CREATE OR REPLACE FUNCTION fn_queue_release(
    p_script_id VARCHAR,
    p_stage     queue_stage,
    p_state     queue_state DEFAULT 'queued',
    p_from      queue_state DEFAULT 'claimed',
    p_requeue_at_end BOOLEAN DEFAULT FALSE
) RETURNS INT AS $$
DECLARE
    v_count INT;
BEGIN
    UPDATE script_queue
       SET state = p_state,
           enqueued_at = CASE WHEN p_requeue_at_end THEN now() ELSE enqueued_at END,
           claimed_by = NULL,
           claimed_at = NULL,
           claim_expires_at = NULL
     WHERE script_id = p_script_id
       AND stage = p_stage
       AND state = p_from;

    GET DIAGNOSTICS v_count = ROW_COUNT;
    RETURN v_count;
END $$ LANGUAGE plpgsql;

-- Đưa câu trở lại hàng đợi ghi âm sau khi bản ghi bị loại: hàng cũ đang 'done' thì
-- chuyển về 'queued', chưa có hàng thì tạo mới. Mốc FIFO đặt lại là lúc bị loại, nên
-- câu bị từ chối được thu lại ở lượt sau chứ không nhảy về đầu hàng đợi cũ.
CREATE OR REPLACE FUNCTION fn_queue_requeue(
    p_script_id VARCHAR,
    p_stage     queue_stage DEFAULT 'recording'
) RETURNS INT AS $$
DECLARE
    v_moved INT;
BEGIN
    v_moved := fn_queue_release(p_script_id, p_stage, 'queued', 'done', TRUE);

    IF v_moved = 0 THEN
        v_moved := fn_queue_release(p_script_id, p_stage, 'queued', 'claimed', TRUE);
    END IF;

    IF v_moved = 0 THEN
        INSERT INTO script_queue (script_id, stage, state)
        VALUES (p_script_id, p_stage, 'queued')
        ON CONFLICT (script_id, stage) WHERE state IN ('queued','claimed')
        DO NOTHING;
        v_moved := 1;
    END IF;

    RETURN v_moved;
END $$ LANGUAGE plpgsql;

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

-- [ERD] pair rule: one script pair = ONE speaker (same take, same session).
-- qc_failed takes do not claim ownership: a failed take by another speaker must not
-- permanently block reuse.
CREATE OR REPLACE FUNCTION fn_recording_single_speaker() RETURNS trigger AS $$ BEGIN
    -- TEAM_007: Khóa cấp giao dịch theo băm script_id để ngăn race condition
    PERFORM pg_advisory_xact_lock(hashtext(NEW.script_id));

    IF EXISTS (SELECT 1 FROM recording r
               WHERE r.script_id = NEW.script_id
                 AND r.speaker_id <> NEW.speaker_id
                 AND r.status <> 'qc_failed') THEN
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

-- [ERD] dataset immutability: cannot modify recordings of a released or archived dataset
CREATE OR REPLACE FUNCTION fn_validate_dataset_recording_modification() RETURNS trigger AS $$
DECLARE
    v_ds_status dataset_status;
    v_ds_id     BIGINT;
BEGIN
    v_ds_id := COALESCE(NEW.dataset_id, OLD.dataset_id);
    SELECT status INTO v_ds_status FROM dataset WHERE dataset_id = v_ds_id;

    IF v_ds_status = 'released' THEN
        RAISE EXCEPTION 'dataset % is already released and cannot be modified', v_ds_id;
    ELSIF v_ds_status = 'archived' THEN
        RAISE EXCEPTION 'dataset % is archived and cannot be modified', v_ds_id;
    END IF;

    RETURN COALESCE(NEW, OLD);
END $$ LANGUAGE plpgsql;

CREATE TRIGGER trg_dataset_recording_immutable
    BEFORE INSERT OR UPDATE OR DELETE ON dataset_recording
    FOR EACH ROW EXECUTE FUNCTION fn_validate_dataset_recording_modification();

-- [ERD] after independent reviews, final status = majority decision (reads review.rounds_required from config)
CREATE OR REPLACE FUNCTION fn_review_majority_status() RETURNS trigger AS $$ DECLARE
    v_rounds_required INT;
    v_total           INT;
    v_approved        INT;
    v_final           recording_status;
BEGIN
    SELECT COALESCE(NULLIF(config_value, '')::int, 3)
      INTO v_rounds_required
      FROM system_config
     WHERE config_key = 'review.rounds_required';

    IF v_rounds_required IS NULL OR v_rounds_required < 1 THEN
        v_rounds_required := 3;
    END IF;

    SELECT COUNT(*), COUNT(*) FILTER (WHERE decision = 'approved')
      INTO v_total, v_approved
      FROM review WHERE recording_id = NEW.recording_id;

    IF v_total >= v_rounds_required THEN
        v_final := CASE WHEN v_approved >= (v_rounds_required / 2) + 1 THEN 'approved' ELSE 'rejected' END;
        UPDATE recording SET status = v_final
         WHERE recording_id = NEW.recording_id AND status = 'pending_review';

        -- TEAM_007: Chỉ khi đa số từ chối bản ghi VÀ có lý do thuộc nhóm 'content' mới trả câu về pending_validation
        IF v_final = 'rejected' THEN
            IF EXISTS (
                SELECT 1
                  FROM review_rejection_reason rrr
                  JOIN rejection_reason rr ON rr.reason_id = rrr.reason_id
                  JOIN review rv ON rv.review_id = rrr.review_id
                 WHERE rv.recording_id = NEW.recording_id
                   AND rr.category = 'content'
            ) THEN
                UPDATE script SET status = 'pending_validation'
                 WHERE script_id = (SELECT script_id FROM recording WHERE recording_id = NEW.recording_id)
                   AND status = 'validated';

                -- TEAM_010: câu quay về bước duyệt nội dung thì không còn là mục công việc
                -- nữa. Trước đây trigger để nguyên task_script ở 'pending' nên task treo
                -- vĩnh viễn, không bao giờ đạt chỉ tiêu.
                UPDATE task_script SET status = 'rejected'
                 WHERE script_id = (SELECT script_id FROM recording WHERE recording_id = NEW.recording_id)
                   AND status = 'pending';
            END IF;
        END IF;
    END IF;

    RETURN NEW;
END $$ LANGUAGE plpgsql;

CREATE TRIGGER trg_review_majority AFTER INSERT ON review
    FOR EACH ROW EXECUTE FUNCTION fn_review_majority_status();

-- [ERD] task-script: domain match and script validated check for recording tasks
CREATE OR REPLACE FUNCTION fn_validate_task_script() RETURNS trigger AS $$
DECLARE
    v_task_type     task_type;
    v_task_domain   script_domain;
    v_script_domain script_domain;
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

CREATE TRIGGER trg_task_script_validation
    BEFORE INSERT OR UPDATE OF task_id, script_id ON task_script
    FOR EACH ROW EXECUTE FUNCTION fn_validate_task_script();

-- [ERD] task assignment role check: speaker for recording task, reviewer for review task
CREATE OR REPLACE FUNCTION fn_validate_task_assignment_role() RETURNS trigger AS $$
DECLARE
    v_task_type task_type;
    v_role_name VARCHAR(32);
    v_status    user_status;
BEGIN
    SELECT task_type INTO v_task_type FROM task WHERE task_id = NEW.task_id;
    IF NOT FOUND THEN
        RAISE EXCEPTION 'task % does not exist', NEW.task_id;
    END IF;

    SELECT r.role_name, u.status INTO v_role_name, v_status
      FROM app_user u
      JOIN role r ON r.role_id = u.role_id
     WHERE u.user_id = NEW.user_id;
    IF NOT FOUND THEN
        RAISE EXCEPTION 'user % does not exist', NEW.user_id;
    END IF;

    IF v_status <> 'active' THEN
        RAISE EXCEPTION 'user % is not active (status: %)', NEW.user_id, v_status;
    END IF;

    IF v_task_type = 'recording' AND v_role_name <> 'speaker' THEN
        RAISE EXCEPTION 'recording task % can only be assigned to a speaker (user % has role %)',
            NEW.task_id, NEW.user_id, v_role_name;
    ELSIF v_task_type = 'review' AND v_role_name <> 'reviewer' THEN
        RAISE EXCEPTION 'review task % can only be assigned to a reviewer (user % has role %)',
            NEW.task_id, NEW.user_id, v_role_name;
    END IF;

    RETURN NEW;
END $$ LANGUAGE plpgsql;

CREATE TRIGGER trg_task_assignment_role
    BEFORE INSERT OR UPDATE OF task_id, user_id ON task_assignment
    FOR EACH ROW EXECUTE FUNCTION fn_validate_task_assignment_role();

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
    ('meaningless',    'Câu không có nghĩa rõ ràng — nên bỏ và tìm câu khác', 10),
    ('unnatural',      'Chuyển ngữ Việt - Anh không tự nhiên — cần nói lại cho nghe như người bản xứ', 20),
    ('grammar',        'Lỗi ngữ pháp trong câu tiếng Anh hoặc tiếng Việt', 30),
    ('spelling',       'Lỗi chính tả trong câu tiếng Anh hoặc tiếng Việt', 40),
    ('mismatch',       'Hai câu không cùng nghĩa — phần tiếng Anh không dịch được sang tiếng Việt', 50),
    ('duplicate',      'Câu này đã có trong dữ liệu — không cần gửi lại', 60),
    ('other',          'Lỗi văn bản khác — ghi rõ ở ô bình luận', 99);

INSERT INTO rejection_reason (reason_code, category, description) VALUES
    ('content_mismatch',    'content',        'Đọc sai nội dung — phần đọc không khớp với văn bản đã cho'),
    ('script_text_error',   'content',        'Văn bản câu bị sai — câu này sẽ quay về bước duyệt nội dung'),
    ('background_noise',    'audio_quality',  'Có tạp âm — nghe thấy tiếng khác lẫn vào trong bản ghi'),
    ('low_volume',          'audio_quality',  'Âm lượng quá nhỏ — người nghe phải vặn to mới nghe rõ'),
    ('clipping_distortion', 'audio_quality',  'Âm thanh bị vỡ — nói quá lớn hoặc thu trong lúc bị va chạm'),
    ('excess_silence',      'audio_quality',  'Khoảng lặng đầu hoặc cuối quá dài — thu lại phần này'),
    ('mispronunciation',    'pronunciation',  'Phát âm sai — đọc chưa đúng, cần tập lại rồi thu'),
    ('word_skipped_added',  'pronunciation',  'Đọc thiếu, thêm hoặc thay từ — phần đọc lệch với văn bản'),
    ('other',               'other',          'Lỗi khác — ghi rõ ở ô bình luận');

INSERT INTO system_config (config_key, config_value, value_type, description) VALUES
    ('review.rounds_required',              '3',     'int',    'Independent reviews per recording'),
    ('review.default_blind',                'true',  'bool',   'Reviews are blind by default'),
    -- [TEAM_001] silence thresholds double as auto-QC limits: exceeding them fails QC at upload
    ('recording.max_leading_silence_sec',   '1',     'int',    'Max silence before speech: auto-QC threshold and speaker guidance'),
    ('recording.max_trailing_silence_sec',  '1',     'int',    'Max silence after speech: auto-QC threshold and speaker guidance'),
    -- [TEAM_001] analyser parameters (group proposal; DatabaseSeeder seeds the same keys for older DBs)
    ('recording.silence_noise_db',          '-35',   'int',    'dB threshold treated as silence by the auto-QC analyser'),
    ('recording.silence_min_duration_sec',  '0.5',   'string', 'Silences shorter than this (seconds) are ignored by the auto-QC analyser'),
    ('recording.audio_format_default',      'wav',   'string', 'Default audio format'),
    ('recording.max_take',                  '99',    'int',    'Highest re-record take number'),
    ('import.max_scripts_per_batch',        '100000','int',    'Sanity cap per import batch'),
    ('recording.min_mean_volume_db',        '-50',   'string', 'Min mean volume (dBFS); below fails auto-QC as too_quiet'),
    ('recording.max_peak_db',               '-1',    'string', 'Max peak (dBFS); above fails auto-QC as clipping_detected'),
    -- anti-junk thresholds: PLACEHOLDER values, confirm with supervisor
    ('quality.speaker_min_approval_rate',        '60', 'int', 'Min approval rate (%) before a speaker is flagged'),
    ('quality.speaker_max_consecutive_qc_fail',  '5',  'int', 'Consecutive qc_failed recordings before warning/suspension'),
    ('quality.min_recordings_before_eval',       '20', 'int', 'Recordings needed before approval rate is evaluated'),
    -- TEAM_010: hạn claim của hàng đợi. Người mở câu rồi bỏ ngang sẽ trả câu về
    -- hàng đợi sau tối đa số phút này, thay vì giữ mãi.
    ('queue.claim_ttl_minutes',                  '30',  'int', 'Minutes before an unclaimed queue item becomes claimable again'),
    -- TEAM_010: lô câu văn bản Task Manager giao cho mỗi người duyệt (yêu cầu 2.2).
    ('text_review.batch_size',                   '100', 'int', 'Default number of pending sentences claimed per reviewer');

-- Bootstrap admin — valid bcrypt hash for initial password 'Codeswitch@2026'
INSERT INTO app_user (role_id, full_name, email, password_hash, status)
SELECT role_id, 'System Admin', 'admin@codeswitchlabel.local',
       '$2a$11$eUoiC4zG9UaJtJ6lY6qZ3.gQ3uRz5i0/7m6Fq5W2qXgC5S5wZ4mO', 'active'
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
          / NULLIF(COUNT(*) FILTER (WHERE r.status IN ('approved', 'rejected')), 0), 1)
                                                                 AS approval_rate_pct
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
    t.domain,
    t.status        AS task_status,
    t.target_qty,
    t.deadline,
    -- TEAM_010: một task có thể giao cho nhiều người. Chỉ tiêu là của cả nhóm nên
    -- không nhân theo số này — chỉ để báo cáo biết đang chia việc cho bao nhiêu người.
    (SELECT COUNT(*) FROM task_assignment a
        WHERE a.task_id = t.task_id AND a.assignment_status = 'active') AS assignee_count,
    (SELECT COUNT(*) FROM task_script ts WHERE ts.task_id = t.task_id) AS scripts_assigned,
    (SELECT COUNT(*) FROM task_script ts
        WHERE ts.task_id = t.task_id AND ts.status = 'completed')      AS scripts_completed,
    (SELECT COUNT(*) FROM review rv WHERE rv.task_id = t.task_id)      AS reviews_done
FROM task t
LEFT JOIN campaign c ON c.campaign_id = t.campaign_id;

-- Campaign-level planning/progress summary
CREATE VIEW v_campaign_progress AS
SELECT
    c.campaign_id,
    c.campaign_name,
    c.domain,
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
    c.domain,
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