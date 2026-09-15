/* ======================================================================
   CodeSwitchLabel — Speech Data Collection & Quality Control System
   Full database install script (single file, single run)  — v1.3
   Target  : PostgreSQL 15+
   Objects : 16 tables, 13 enum types, partitioned audit_log,
             partial indexes, 18 triggers (10 guards, 2 auto-status,
             4 audit, 2 touch), 4 views
   Data    : 13 accounts (all roles; team members use fictional identities)
             + demo corpus (8 sentences, 3 tasks, 10 recordings,
               16 reviews incl. 2-stage flow, 2 datasets)
               + self-testing smoke test (6 negative guard tests)
   Safety  : DESTRUCTIVE on re-run (drops schema 'csl' and rebuilds it)
   Usage   : psql -U postgres -d <db> -f codeswitchlabel_schema.sql
   Upgrade : the blocks marked "v1.3" (2b and the last three trigger
             functions) can be applied alone to a live v1.2 database;
             everything else is unchanged from v1.2
   ====================================================================== */

-- ---------------------------------------------------------------- 0. guard
DO $$ BEGIN
  IF current_setting('server_version_num')::int < 150000 THEN
    RAISE EXCEPTION 'PostgreSQL 15 or newer is required';
  END IF;
END $$;

BEGIN;

-- ---------------------------------------------------------------- 0. reset
DROP SCHEMA IF EXISTS csl CASCADE;
CREATE SCHEMA csl;
COMMENT ON SCHEMA csl IS 'CodeSwitchLabel: Vietnamese-English code-switched speech corpus system';
SET search_path TO csl, public;

-- Optional trigram search (skip gracefully if unavailable)
DO $$ BEGIN
  EXECUTE 'CREATE EXTENSION IF NOT EXISTS pg_trgm SCHEMA public';
  RAISE NOTICE 'pg_trgm extension ready';
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'pg_trgm unavailable (%) — trigram search index skipped', SQLERRM;
END $$;

-- pgcrypto: bcrypt hashing for seed/demo account passwords.
-- Demo password for ALL seeded accounts: Password123!
DO $$ BEGIN
  EXECUTE 'CREATE EXTENSION IF NOT EXISTS pgcrypto SCHEMA public';
  PERFORM set_config('csl.demo_hash',
                     crypt('Password123!', gen_salt('bf', 10)), false);
  RAISE NOTICE 'pgcrypto ready — demo passwords are bcrypt hashes of Password123!';
EXCEPTION WHEN OTHERS THEN
  PERFORM set_config('csl.demo_hash', 'RESET_ME_NO_PGCRYPTO', false);
  RAISE NOTICE 'pgcrypto unavailable (%) — password hashes are placeholders; reset them before first login', SQLERRM;
END $$;

-- ---------------------------------------------------------------- 1. enums
CREATE TYPE csl.role_name_t          AS ENUM ('speaker','reviewer','task_manager','admin');
CREATE TYPE csl.user_status_t        AS ENUM ('active','inactive');
CREATE TYPE csl.sentence_source_t    AS ENUM ('admin_import','speaker_contribution');
CREATE TYPE csl.sentence_status_t    AS ENUM ('pending_validation','validated','rejected');
CREATE TYPE csl.validation_result_t  AS ENUM ('accepted','edited','rejected');
CREATE TYPE csl.recording_status_t   AS ENUM ('pending_review','approved','rejected','superseded');
CREATE TYPE csl.review_decision_t    AS ENUM ('approved','rejected');
CREATE TYPE csl.rejection_category_t AS ENUM ('content','audio_quality','pronunciation','other');
CREATE TYPE csl.task_type_t          AS ENUM ('recording','review');
CREATE TYPE csl.task_status_t        AS ENUM ('draft','open','in_progress','completed','cancelled');
CREATE TYPE csl.assignment_status_t  AS ENUM ('active','completed','reassigned','cancelled');
CREATE TYPE csl.dataset_status_t     AS ENUM ('draft','released','archived');
CREATE TYPE csl.audit_action_t       AS ENUM ('create','update','delete','import','export','assign','release','login');

CREATE SEQUENCE csl.audit_log_id_seq;

-- ---------------------------------------------------------------- 2. tables
CREATE TABLE csl.role (
  role_id     smallint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  role_name   csl.role_name_t NOT NULL UNIQUE,
  description text
);

CREATE TABLE csl.app_user (
  user_id       bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  role_id       smallint NOT NULL REFERENCES csl.role(role_id),
  full_name     varchar(100) NOT NULL,
  email         varchar(255) NOT NULL UNIQUE,
  phone         varchar(20),
  password_hash text NOT NULL,
  status        csl.user_status_t NOT NULL DEFAULT 'active',
  created_at    timestamptz NOT NULL DEFAULT now(),
  updated_at    timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE csl.sentence (
  sentence_id     bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  content         text NOT NULL,
  source          csl.sentence_source_t NOT NULL,
  status          csl.sentence_status_t NOT NULL DEFAULT 'pending_validation',
  has_code_switch boolean NOT NULL DEFAULT true,
  created_by      bigint NOT NULL REFERENCES csl.app_user(user_id),
  created_at      timestamptz NOT NULL DEFAULT now(),
  updated_at      timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE csl.text_validation (
  sentence_id       bigint NOT NULL REFERENCES csl.sentence(sentence_id),
  validator_id      bigint NOT NULL REFERENCES csl.app_user(user_id),
  validation_result csl.validation_result_t NOT NULL,
  edited_content    text,
  comment           text,
  validated_at      timestamptz NOT NULL DEFAULT now(),
  PRIMARY KEY (sentence_id, validator_id)
);

CREATE TABLE csl.task (
  task_id     bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  created_by  bigint NOT NULL REFERENCES csl.app_user(user_id),
  task_type   csl.task_type_t NOT NULL,
  title       varchar(200) NOT NULL,
  description text,
  target_qty  integer NOT NULL CHECK (target_qty > 0),
  deadline    timestamptz NOT NULL,
  status      csl.task_status_t NOT NULL DEFAULT 'draft',
  created_at  timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE csl.task_assignment (
  assignment_id     bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  task_id           bigint NOT NULL REFERENCES csl.task(task_id),
  user_id           bigint NOT NULL REFERENCES csl.app_user(user_id),
  assigned_at       timestamptz NOT NULL DEFAULT now(),
  assignment_status csl.assignment_status_t NOT NULL DEFAULT 'active'
);

CREATE TABLE csl.task_sentence (
  task_id     bigint NOT NULL REFERENCES csl.task(task_id),
  sentence_id bigint NOT NULL REFERENCES csl.sentence(sentence_id),
  included_at timestamptz NOT NULL DEFAULT now(),
  PRIMARY KEY (task_id, sentence_id)
);

CREATE TABLE csl.recording (
  recording_id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  sentence_id  bigint NOT NULL REFERENCES csl.sentence(sentence_id),
  speaker_id   bigint NOT NULL REFERENCES csl.app_user(user_id),
  task_id      bigint REFERENCES csl.task(task_id),
  storage_key  text NOT NULL UNIQUE,
  duration_sec numeric(8,2) NOT NULL CHECK (duration_sec > 0),
  audio_format varchar(10) NOT NULL DEFAULT 'wav',
  status       csl.recording_status_t NOT NULL DEFAULT 'pending_review',
  attempt_no   integer NOT NULL CHECK (attempt_no >= 1),
  recorded_at  timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE csl.task_recording (
  task_id      bigint NOT NULL REFERENCES csl.task(task_id),
  recording_id bigint NOT NULL REFERENCES csl.recording(recording_id),
  included_at  timestamptz NOT NULL DEFAULT now(),
  PRIMARY KEY (task_id, recording_id)
);

CREATE TABLE csl.review (
  review_id    bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  recording_id bigint NOT NULL REFERENCES csl.recording(recording_id),
  reviewer_id  bigint NOT NULL REFERENCES csl.app_user(user_id),
  task_id      bigint REFERENCES csl.task(task_id),
  stage        integer NOT NULL DEFAULT 1 CHECK (stage >= 1),
  decision     csl.review_decision_t NOT NULL,
  comment      text,
  reviewed_at  timestamptz NOT NULL DEFAULT now(),
  UNIQUE (recording_id, reviewer_id, stage)
);

CREATE TABLE csl.rejection_reason (
  reason_id   smallint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  reason_code varchar(30) NOT NULL UNIQUE,
  category    csl.rejection_category_t NOT NULL,
  description text,
  is_active   boolean NOT NULL DEFAULT true
);

CREATE TABLE csl.review_rejection_reason (
  review_id bigint   NOT NULL REFERENCES csl.review(review_id),
  reason_id smallint NOT NULL REFERENCES csl.rejection_reason(reason_id),
  PRIMARY KEY (review_id, reason_id)
);

CREATE TABLE csl.dataset (
  dataset_id   bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  dataset_name varchar(100) NOT NULL,
  version      varchar(20) NOT NULL,
  description  text,
  status       csl.dataset_status_t NOT NULL DEFAULT 'draft',
  released_by  bigint REFERENCES csl.app_user(user_id),
  released_at  timestamptz,
  created_at   timestamptz NOT NULL DEFAULT now(),
  UNIQUE (dataset_name, version)
);

CREATE TABLE csl.dataset_recording (
  dataset_id   bigint NOT NULL REFERENCES csl.dataset(dataset_id),
  recording_id bigint NOT NULL REFERENCES csl.recording(recording_id),
  included_at  timestamptz NOT NULL DEFAULT now(),
  PRIMARY KEY (dataset_id, recording_id)
);

CREATE TABLE csl.system_config (
  config_id    smallint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  config_key   varchar(100) NOT NULL UNIQUE,
  config_value text NOT NULL,
  value_type   varchar(10) NOT NULL DEFAULT 'string',
  description  text,
  updated_by   bigint REFERENCES csl.app_user(user_id),
  updated_at   timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE csl.audit_log (
  audit_id    bigint NOT NULL DEFAULT nextval('csl.audit_log_id_seq'),
  user_id     bigint REFERENCES csl.app_user(user_id),
  entity_type varchar(30) NOT NULL,
  entity_id   bigint NOT NULL,
  action      csl.audit_action_t NOT NULL,
  old_value   jsonb,
  new_value   jsonb,
  changed_at  timestamptz NOT NULL DEFAULT now(),
  PRIMARY KEY (audit_id, changed_at)
) PARTITION BY RANGE (changed_at);

CREATE TABLE csl.audit_log_p2026m09 PARTITION OF csl.audit_log FOR VALUES FROM ('2026-09-01') TO ('2026-10-01');
CREATE TABLE csl.audit_log_p2026m10 PARTITION OF csl.audit_log FOR VALUES FROM ('2026-10-01') TO ('2026-11-01');
CREATE TABLE csl.audit_log_p2026m11 PARTITION OF csl.audit_log FOR VALUES FROM ('2026-11-01') TO ('2026-12-01');
CREATE TABLE csl.audit_log_p2026m12 PARTITION OF csl.audit_log FOR VALUES FROM ('2026-12-01') TO ('2027-01-01');
CREATE TABLE csl.audit_log_p2027m01 PARTITION OF csl.audit_log FOR VALUES FROM ('2027-01-01') TO ('2027-02-01');
CREATE TABLE csl.audit_log_p2027m02 PARTITION OF csl.audit_log FOR VALUES FROM ('2027-02-01') TO ('2027-03-01');
CREATE TABLE csl.audit_log_p2027m03 PARTITION OF csl.audit_log FOR VALUES FROM ('2027-03-01') TO ('2027-04-01');
CREATE TABLE csl.audit_log_default  PARTITION OF csl.audit_log DEFAULT;

-- ------------------------------------------------ 2b. v1.3 integrity hardening
-- A. attempt numbering is per (sentence, speaker): no duplicate attempt_no
ALTER TABLE csl.recording
  ADD CONSTRAINT uq_rec_attempt UNIQUE (sentence_id, speaker_id, attempt_no);

-- B. a released dataset must record who released it and when
ALTER TABLE csl.dataset
  ADD CONSTRAINT chk_dataset_release CHECK (
    status <> 'released' OR (released_by IS NOT NULL AND released_at IS NOT NULL));

-- C. edited_content exists iff the validator edited the text
ALTER TABLE csl.text_validation
  ADD CONSTRAINT chk_tv_edited CHECK (
    (validation_result = 'edited') = (edited_content IS NOT NULL));

-- ---------------------------------------------------------------- 3. indexes
CREATE INDEX idx_user_role           ON csl.app_user(role_id);
CREATE INDEX idx_sentence_created_by ON csl.sentence(created_by);
CREATE INDEX idx_textval_validator   ON csl.text_validation(validator_id);
CREATE INDEX idx_task_created_by     ON csl.task(created_by);
CREATE INDEX idx_assign_user         ON csl.task_assignment(user_id);
CREATE INDEX idx_tasksent_sentence   ON csl.task_sentence(sentence_id);
CREATE INDEX idx_rec_sentence        ON csl.recording(sentence_id);
CREATE INDEX idx_rec_speaker         ON csl.recording(speaker_id);
CREATE INDEX idx_rec_task            ON csl.recording(task_id);
CREATE INDEX idx_taskrec_recording   ON csl.task_recording(recording_id);
CREATE INDEX idx_review_recording    ON csl.review(recording_id);
CREATE INDEX idx_review_reviewer     ON csl.review(reviewer_id);
CREATE INDEX idx_review_task         ON csl.review(task_id);
CREATE INDEX idx_rrr_reason          ON csl.review_rejection_reason(reason_id);
CREATE INDEX idx_dsrec_recording     ON csl.dataset_recording(recording_id);
CREATE INDEX idx_dataset_released_by ON csl.dataset(released_by);
CREATE INDEX idx_audit_user          ON csl.audit_log(user_id, changed_at);
CREATE INDEX idx_audit_entity        ON csl.audit_log(entity_type, entity_id);

-- v1.3: partial/typed keys instead of low-selectivity b-trees
CREATE INDEX idx_rec_pending        ON csl.recording(recorded_at) WHERE status = 'pending_review';
CREATE INDEX idx_task_open_deadline ON csl.task(deadline)         WHERE status IN ('open','in_progress');
CREATE INDEX idx_review_rejected    ON csl.review(recording_id)   WHERE decision = 'rejected';
CREATE INDEX idx_sentence_pending   ON csl.sentence(created_at)   WHERE status = 'pending_validation';

CREATE UNIQUE INDEX uq_task_active_assignment ON csl.task_assignment(task_id)
  WHERE assignment_status = 'active';

DO $$ BEGIN
  IF EXISTS (SELECT 1 FROM pg_catalog.pg_extension WHERE extname = 'pg_trgm') THEN
    EXECUTE 'CREATE INDEX idx_sentence_trgm ON csl.sentence USING GIN (content gin_trgm_ops)';
  END IF;
END $$;

-- ---------------------------------------------------------------- 4. triggers
CREATE OR REPLACE FUNCTION csl.chk_reviewer_not_speaker() RETURNS trigger AS $$ BEGIN
  IF EXISTS (SELECT 1 FROM csl.recording r
             WHERE r.recording_id = NEW.recording_id
               AND r.speaker_id   = NEW.reviewer_id) THEN
    RAISE EXCEPTION 'Reviewer % is the speaker of recording % — self-review not allowed',
                    NEW.reviewer_id, NEW.recording_id;
  END IF;
  RETURN NEW;
END $$ LANGUAGE plpgsql;
CREATE TRIGGER trg_review_not_self BEFORE INSERT OR UPDATE ON csl.review
FOR EACH ROW EXECUTE FUNCTION csl.chk_reviewer_not_speaker();

CREATE OR REPLACE FUNCTION csl.chk_rejection_has_reason() RETURNS trigger AS $$ DECLARE v_review bigint;
BEGIN
  v_review := CASE WHEN TG_OP = 'DELETE' THEN OLD.review_id ELSE NEW.review_id END;
  IF (SELECT decision FROM csl.review WHERE review_id = v_review) = 'rejected'
     AND NOT EXISTS (SELECT 1 FROM csl.review_rejection_reason WHERE review_id = v_review) THEN
    RAISE EXCEPTION 'Rejected review % must cite at least one rejection reason', v_review;
  END IF;
  RETURN NULL;
END $$ LANGUAGE plpgsql;
CREATE CONSTRAINT TRIGGER trg_rejection_reason_required
AFTER INSERT ON csl.review DEFERRABLE INITIALLY DEFERRED
FOR EACH ROW EXECUTE FUNCTION csl.chk_rejection_has_reason();
CREATE CONSTRAINT TRIGGER trg_rejection_reason_required_ii
AFTER INSERT OR DELETE ON csl.review_rejection_reason DEFERRABLE INITIALLY DEFERRED
FOR EACH ROW EXECUTE FUNCTION csl.chk_rejection_has_reason();

CREATE OR REPLACE FUNCTION csl.chk_sentence_validated() RETURNS trigger AS $$ BEGIN
  IF (SELECT status FROM csl.sentence WHERE sentence_id = NEW.sentence_id) <> 'validated' THEN
    RAISE EXCEPTION 'Sentence % is not validated — recording not allowed', NEW.sentence_id;
  END IF;
  RETURN NEW;
END $$ LANGUAGE plpgsql;
CREATE TRIGGER trg_rec_sentence_validated BEFORE INSERT ON csl.recording
FOR EACH ROW EXECUTE FUNCTION csl.chk_sentence_validated();

CREATE OR REPLACE FUNCTION csl.chk_dataset_recording_approved() RETURNS trigger AS $$ BEGIN
  IF (SELECT status FROM csl.recording WHERE recording_id = NEW.recording_id) <> 'approved' THEN
    RAISE EXCEPTION 'Recording % is not approved — dataset inclusion denied', NEW.recording_id;
  END IF;
  RETURN NEW;
END $$ LANGUAGE plpgsql;
CREATE TRIGGER trg_dsrec_approved BEFORE INSERT ON csl.dataset_recording
FOR EACH ROW EXECUTE FUNCTION csl.chk_dataset_recording_approved();

CREATE OR REPLACE FUNCTION csl.chk_assignment_role() RETURNS trigger AS $$ BEGIN
  IF NOT EXISTS (SELECT 1
                 FROM csl.task t
                 JOIN csl.app_user u ON u.user_id = NEW.user_id
                 JOIN csl.role    r ON r.role_id = u.role_id
                 WHERE t.task_id = NEW.task_id
                   AND ((t.task_type = 'recording' AND r.role_name = 'speaker')
                     OR (t.task_type = 'review'     AND r.role_name = 'reviewer'))) THEN
    RAISE EXCEPTION 'Assignee role does not match task type (task %, user %)', NEW.task_id, NEW.user_id;
  END IF;
  RETURN NEW;
END $$ LANGUAGE plpgsql;
CREATE TRIGGER trg_assign_role_match BEFORE INSERT ON csl.task_assignment
FOR EACH ROW EXECUTE FUNCTION csl.chk_assignment_role();

CREATE OR REPLACE FUNCTION csl.chk_tasksent_type() RETURNS trigger AS $$ BEGIN
  IF (SELECT task_type FROM csl.task WHERE task_id = NEW.task_id) <> 'recording' THEN
    RAISE EXCEPTION 'Only recording tasks may list sentences (task %)', NEW.task_id;
  END IF;
  RETURN NEW;
END $$ LANGUAGE plpgsql;
CREATE TRIGGER trg_tasksent_type BEFORE INSERT ON csl.task_sentence
FOR EACH ROW EXECUTE FUNCTION csl.chk_tasksent_type();

CREATE OR REPLACE FUNCTION csl.chk_taskrec_type() RETURNS trigger AS $$ BEGIN
  IF (SELECT task_type FROM csl.task WHERE task_id = NEW.task_id) <> 'review' THEN
    RAISE EXCEPTION 'Only review tasks may list recordings (task %)', NEW.task_id;
  END IF;
  RETURN NEW;
END $$ LANGUAGE plpgsql;
CREATE TRIGGER trg_taskrec_type BEFORE INSERT ON csl.task_recording
FOR EACH ROW EXECUTE FUNCTION csl.chk_taskrec_type();

CREATE OR REPLACE FUNCTION csl.chk_rec_status_flow() RETURNS trigger AS $$ BEGIN
  IF NEW.status <> OLD.status AND NOT (
       (OLD.status = 'pending_review' AND NEW.status IN ('approved','rejected','superseded'))
    OR (OLD.status IN ('approved','rejected') AND NEW.status = 'superseded')) THEN
    RAISE EXCEPTION 'Invalid recording status transition % -> %', OLD.status, NEW.status;
  END IF;
  RETURN NEW;
END $$ LANGUAGE plpgsql;
CREATE TRIGGER trg_rec_status_flow BEFORE UPDATE ON csl.recording
FOR EACH ROW EXECUTE FUNCTION csl.chk_rec_status_flow();

CREATE OR REPLACE FUNCTION csl.fn_audit_row() RETURNS trigger AS $$ DECLARE v_user bigint; v_id bigint;
  v_action csl.audit_action_t := CASE TG_OP WHEN 'INSERT' THEN 'create'
                                            WHEN 'UPDATE' THEN 'update'
                                            WHEN 'DELETE' THEN 'delete' END;
BEGIN
  v_user := NULLIF(current_setting('app.current_user', true), '')::bigint;
  EXECUTE format('SELECT ($1).%I', TG_ARGV[0]) INTO v_id USING COALESCE(NEW, OLD);
  INSERT INTO csl.audit_log (user_id, entity_type, entity_id, action, old_value, new_value)
  VALUES (v_user, TG_TABLE_NAME, v_id, v_action,
          CASE WHEN TG_OP <> 'INSERT' THEN to_jsonb(OLD) END,
          CASE WHEN TG_OP <> 'DELETE' THEN to_jsonb(NEW) END);
  RETURN COALESCE(NEW, OLD);
END $$ LANGUAGE plpgsql;
CREATE TRIGGER trg_audit_sentence  AFTER INSERT OR UPDATE OR DELETE ON csl.sentence
FOR EACH ROW EXECUTE FUNCTION csl.fn_audit_row('sentence_id');
CREATE TRIGGER trg_audit_recording AFTER INSERT OR UPDATE OR DELETE ON csl.recording
FOR EACH ROW EXECUTE FUNCTION csl.fn_audit_row('recording_id');
CREATE TRIGGER trg_audit_task      AFTER INSERT OR UPDATE OR DELETE ON csl.task
FOR EACH ROW EXECUTE FUNCTION csl.fn_audit_row('task_id');
CREATE TRIGGER trg_audit_config    AFTER INSERT OR UPDATE OR DELETE ON csl.system_config
FOR EACH ROW EXECUTE FUNCTION csl.fn_audit_row('config_id');

CREATE OR REPLACE FUNCTION csl.fn_touch() RETURNS trigger AS $$ BEGIN NEW.updated_at := now(); RETURN NEW; END $$ LANGUAGE plpgsql;
CREATE TRIGGER trg_touch_app_user BEFORE UPDATE ON csl.app_user
FOR EACH ROW EXECUTE FUNCTION csl.fn_touch();
CREATE TRIGGER trg_touch_sentence  BEFORE UPDATE ON csl.sentence
FOR EACH ROW EXECUTE FUNCTION csl.fn_touch();

-- v1.3 D. rejection reasons may only be cited on REJECTED reviews
CREATE OR REPLACE FUNCTION csl.chk_reason_only_rejected() RETURNS trigger AS $$ BEGIN
  IF (SELECT decision FROM csl.review WHERE review_id = NEW.review_id) <> 'rejected' THEN
    RAISE EXCEPTION 'Review % is approved — rejection reasons not allowed', NEW.review_id;
  END IF;
  RETURN NEW;
END $$ LANGUAGE plpgsql;
CREATE TRIGGER trg_reason_only_rejected BEFORE INSERT ON csl.review_rejection_reason
FOR EACH ROW EXECUTE FUNCTION csl.chk_reason_only_rejected();

-- v1.3 E. first text-validation decision drives sentence status.
-- Rule: FIRST DECISION WINS — later validations are kept as history but
-- never flip (or re-edit) a sentence already decided, so a sentence that
-- entered the recording pipeline cannot mutate underneath it.
CREATE OR REPLACE FUNCTION csl.fn_sentence_status_from_validation() RETURNS trigger AS $$ DECLARE v_status csl.sentence_status_t;
BEGIN
  IF (SELECT status FROM csl.sentence WHERE sentence_id = NEW.sentence_id) = 'pending_validation' THEN
    v_status := CASE WHEN NEW.validation_result IN ('accepted','edited')
                     THEN 'validated' ELSE 'rejected' END;
    IF NEW.validation_result = 'edited' THEN
      UPDATE csl.sentence SET content = NEW.edited_content, status = v_status
      WHERE sentence_id = NEW.sentence_id;
    ELSE
      UPDATE csl.sentence SET status = v_status WHERE sentence_id = NEW.sentence_id;
    END IF;
  END IF;
  RETURN NEW;
END $$ LANGUAGE plpgsql;
CREATE TRIGGER trg_sentence_status AFTER INSERT ON csl.text_validation
FOR EACH ROW EXECUTE FUNCTION csl.fn_sentence_status_from_validation();

-- v1.3 F. review decisions drive recording status:
--   any rejection            -> recording 'rejected'
--   approved at every stage  -> recording 'approved'   (review.stages config)
-- Updates only fire from 'pending_review', so the manual state machine
-- (superseded transitions) is never bypassed. 'review.stages' must be a
-- positive integer; status is evaluated at review time.
CREATE OR REPLACE FUNCTION csl.fn_recording_status_from_review() RETURNS trigger AS $$ DECLARE v_stages integer; v_ok_stages integer;
BEGIN
  SELECT coalesce(max(config_value::int), 1) INTO v_stages
  FROM csl.system_config WHERE config_key = 'review.stages';
  v_stages := greatest(v_stages, 1);

  IF NEW.decision = 'rejected' THEN
    UPDATE csl.recording SET status = 'rejected'
    WHERE recording_id = NEW.recording_id AND status = 'pending_review';
  ELSE
    SELECT count(DISTINCT stage) INTO v_ok_stages
    FROM csl.review WHERE recording_id = NEW.recording_id AND decision = 'approved';
    IF v_ok_stages >= v_stages THEN
      UPDATE csl.recording SET status = 'approved'
      WHERE recording_id = NEW.recording_id AND status = 'pending_review';
    END IF;
  END IF;
  RETURN NEW;
END $$ LANGUAGE plpgsql;
CREATE TRIGGER trg_recording_status AFTER INSERT ON csl.review
FOR EACH ROW EXECUTE FUNCTION csl.fn_recording_status_from_review();

-- ---------------------------------------------------------------- 5. views
CREATE OR REPLACE VIEW csl.v_task_progress AS
SELECT t.task_id, t.title, t.task_type, t.status AS task_status,
       ta.user_id AS assignee_id, u.full_name AS assignee_name,
       t.target_qty, t.deadline, t.deadline - now() AS time_remaining,
       done.done_items,
       ROUND(100.0 * done.done_items / NULLIF(t.target_qty, 0), 1) AS progress_pct
FROM csl.task t
JOIN csl.task_assignment ta ON ta.task_id = t.task_id AND ta.assignment_status = 'active'
JOIN csl.app_user u ON u.user_id = ta.user_id
CROSS JOIN LATERAL (
  SELECT (CASE t.task_type
            WHEN 'recording' THEN (SELECT count(*) FROM csl.recording r
                                   WHERE r.task_id = t.task_id AND r.status <> 'superseded')
            -- v1.3: multi-stage reviews must not double-count progress
            WHEN 'review'     THEN (SELECT count(DISTINCT rv.recording_id) FROM csl.review rv WHERE rv.task_id = t.task_id)
          END)::integer AS done_items) done
WHERE t.status IN ('open','in_progress');

CREATE OR REPLACE VIEW csl.v_speaker_performance AS
SELECT u.user_id, u.full_name,
       count(*)                                      AS total_recordings,
       count(*) FILTER (WHERE r.status = 'approved') AS approved,
       count(*) FILTER (WHERE r.status = 'rejected') AS rejected,
       ROUND(100.0 * count(*) FILTER (WHERE r.status = 'approved')
             / NULLIF(count(*) FILTER (WHERE r.status IN ('approved','rejected')), 0), 1) AS approval_rate_pct
FROM csl.app_user u
JOIN csl.recording r ON r.speaker_id = u.user_id
GROUP BY u.user_id, u.full_name;

CREATE OR REPLACE VIEW csl.v_top_rejection_reasons AS
SELECT rr.reason_code, rr.category, count(*) AS times_cited
FROM csl.review_rejection_reason vrr
JOIN csl.review rv ON rv.review_id = vrr.review_id AND rv.decision = 'rejected'
JOIN csl.rejection_reason rr ON rr.reason_id = vrr.reason_id
GROUP BY rr.reason_code, rr.category
ORDER BY times_cited DESC;

CREATE OR REPLACE VIEW csl.v_dashboard_summary AS
SELECT (SELECT count(*) FROM csl.sentence)                             AS total_sentences,
       (SELECT count(*) FROM csl.sentence WHERE status = 'validated')  AS validated_sentences,
       (SELECT count(*) FROM csl.recording WHERE status = 'approved')  AS approved_recordings,
       (SELECT count(*) FROM csl.recording WHERE status = 'rejected')  AS rejected_recordings,
       (SELECT count(*) FROM csl.recording WHERE status = 'pending_review') AS pending_recordings,
       (SELECT coalesce(sum(duration_sec),0) FROM csl.recording
          WHERE status = 'approved')                                   AS approved_duration_sec,
       (SELECT count(*) FROM csl.app_user u JOIN csl.role r ON r.role_id = u.role_id
          WHERE r.role_name = 'speaker'  AND u.status = 'active')      AS active_speakers,
       (SELECT count(*) FROM csl.app_user u JOIN csl.role r ON r.role_id = u.role_id
          WHERE r.role_name = 'reviewer' AND u.status = 'active')      AS active_reviewers,
       (SELECT count(*) FROM csl.dataset WHERE status = 'released')    AS released_datasets;

-- ================================================ 6. seed: reference + accounts
INSERT INTO csl.role (role_name, description) VALUES
 ('speaker',      'Creates speech data'),
 ('reviewer',     'Reviews and controls recording quality'),
 ('task_manager', 'Assigns tasks, targets and deadlines; monitors progress'),
 ('admin',        'Manages data, users, configuration and statistics');

INSERT INTO csl.rejection_reason (reason_code, category, description) VALUES
 ('OFF_SCRIPT',        'content',        'Recording does not faithfully follow the sentence'),
 ('MISSPOKEN',         'content',        'Speaker stumbled or restarted mid-utterance'),
 ('BACKGROUND_NOISE',  'audio_quality',  'Noise, clipping or low signal-to-noise ratio'),
 ('LOW_VOLUME',        'audio_quality',  'Recording too quiet or distorted'),
 ('MISPRONUNCIATION',  'pronunciation',  'English or Vietnamese word mispronounced'),
 ('ACCENT_TOO_STRONG', 'pronunciation',  'Pronunciation unsuitable for the corpus'),
 ('OTHER',             'other',          'Issue not covered by other codes');

-- 13 accounts: bootstrap admin, fictional team members, and generic QA accounts.
-- Password for every account below: Password123!  (bcrypt, hashed at install)
INSERT INTO csl.app_user (role_id, full_name, email, phone, password_hash) VALUES
((SELECT role_id FROM csl.role WHERE role_name = 'admin'),        'System Administrator',  'admin@codeswitchlabel.local',  NULL,         current_setting('csl.demo_hash')),
((SELECT role_id FROM csl.role WHERE role_name = 'admin'),        'Lê Ngọc Diệp',    'diepln@codeswitchlabel.local',  NULL,         current_setting('csl.demo_hash')),
((SELECT role_id FROM csl.role WHERE role_name = 'task_manager'), 'Trần Quốc Bảo',   'baotq@codeswitchlabel.local',   '0312345487', current_setting('csl.demo_hash')),
((SELECT role_id FROM csl.role WHERE role_name = 'reviewer'),     'Phạm Đức Thắng',  'thangpd@codeswitchlabel.local', '0911234485', current_setting('csl.demo_hash')),
((SELECT role_id FROM csl.role WHERE role_name = 'reviewer'),     'Vũ Nhật Minh',    'minhv@codeswitchlabel.local',   '0361234205', current_setting('csl.demo_hash')),
((SELECT role_id FROM csl.role WHERE role_name = 'speaker'),      'Hoàng Thiên Sơn', 'sonht@codeswitchlabel.local',   '0934123441', current_setting('csl.demo_hash')),
((SELECT role_id FROM csl.role WHERE role_name = 'speaker'),      'Demo Speaker 01',       'speaker01@demo.local',         NULL,         current_setting('csl.demo_hash')),
((SELECT role_id FROM csl.role WHERE role_name = 'speaker'),      'Demo Speaker 02',       'speaker02@demo.local',         NULL,         current_setting('csl.demo_hash')),
((SELECT role_id FROM csl.role WHERE role_name = 'speaker'),      'Demo Speaker 03',       'speaker03@demo.local',         NULL,         current_setting('csl.demo_hash')),
((SELECT role_id FROM csl.role WHERE role_name = 'speaker'),      'Demo Speaker 04',       'speaker04@demo.local',         NULL,         current_setting('csl.demo_hash')),
((SELECT role_id FROM csl.role WHERE role_name = 'reviewer'),     'Demo Reviewer 01',      'reviewer01@demo.local',        NULL,         current_setting('csl.demo_hash')),
((SELECT role_id FROM csl.role WHERE role_name = 'reviewer'),     'Demo Reviewer 02',      'reviewer02@demo.local',        NULL,         current_setting('csl.demo_hash')),
((SELECT role_id FROM csl.role WHERE role_name = 'task_manager'), 'Demo Task Manager',     'taskmanager01@demo.local',     NULL,         current_setting('csl.demo_hash'));

-- attribute the following seed/demo activity to the admin in the audit trail
SELECT set_config('app.current_user',
                  (SELECT user_id::text FROM csl.app_user WHERE email = 'diepln@codeswitchlabel.local'),
                  true);

INSERT INTO csl.system_config (config_key, config_value, value_type, description, updated_by) VALUES
 ('recording.min_duration_sec',   '1',    'int',    'Minimum utterance length',
  (SELECT user_id FROM csl.app_user WHERE email = 'diepln@codeswitchlabel.local')),
 ('recording.max_duration_sec',   '30',   'int',    'Maximum utterance length',
  (SELECT user_id FROM csl.app_user WHERE email = 'diepln@codeswitchlabel.local')),
 ('review.stages',                '2',    'int',    'Number of review stages per recording',
  (SELECT user_id FROM csl.app_user WHERE email = 'diepln@codeswitchlabel.local')),
 ('review.random_ratio',          '0.20', 'string', 'Share of recordings sampled for random review',
  (SELECT user_id FROM csl.app_user WHERE email = 'diepln@codeswitchlabel.local')),
 ('sentence.require_code_switch', 'true', 'bool',   'Contributed sentences must contain code-switching',
  (SELECT user_id FROM csl.app_user WHERE email = 'diepln@codeswitchlabel.local'));

-- ================================================ 7. demo corpus (persistent)
-- 8 sentences: 6 validated, 1 pending validation, 1 rejected (no code-switch)
INSERT INTO csl.sentence (content, source, has_code_switch, created_by) VALUES
('Em nhớ upload tài liệu trước deadline nhé',       'admin_import',         true,  (SELECT user_id FROM csl.app_user WHERE email = 'admin@codeswitchlabel.local')),
('Mình sẽ update report sau meeting nhé',           'admin_import',         true,  (SELECT user_id FROM csl.app_user WHERE email = 'admin@codeswitchlabel.local')),
('Họ đang test version mới của app nhé',            'admin_import',         true,  (SELECT user_id FROM csl.app_user WHERE email = 'admin@codeswitchlabel.local')),
('Bạn check email giúp mình nhé',                   'speaker_contribution', true,  (SELECT user_id FROM csl.app_user WHERE email = 'speaker01@demo.local')),
('Team mình đã deploy feature lên production',      'speaker_contribution', true,  (SELECT user_id FROM csl.app_user WHERE email = 'sonht@codeswitchlabel.local')),
('Anh ấy share screen để demo sản phẩm',            'admin_import',         true,  (SELECT user_id FROM csl.app_user WHERE email = 'admin@codeswitchlabel.local')),
('Chúng ta cần review code trước khi merge branch', 'speaker_contribution', true,  (SELECT user_id FROM csl.app_user WHERE email = 'speaker01@demo.local')),
('Cô ấy gửi file qua Drive rồi',                    'speaker_contribution', false, (SELECT user_id FROM csl.app_user WHERE email = 'speaker02@demo.local'));

-- text validation history (speakers review the text, per requirements)
INSERT INTO csl.text_validation (sentence_id, validator_id, validation_result, edited_content, comment) VALUES
((SELECT sentence_id FROM csl.sentence WHERE content = 'Em nhớ upload tài liệu trước deadline nhé'),
 (SELECT user_id FROM csl.app_user WHERE email = 'speaker01@demo.local'), 'accepted', NULL, NULL),
((SELECT sentence_id FROM csl.sentence WHERE content = 'Mình sẽ update report sau meeting nhé'),
 (SELECT user_id FROM csl.app_user WHERE email = 'speaker01@demo.local'), 'accepted', NULL, NULL),
((SELECT sentence_id FROM csl.sentence WHERE content = 'Họ đang test version mới của app nhé'),
 (SELECT user_id FROM csl.app_user WHERE email = 'sonht@codeswitchlabel.local'), 'edited',
 'Họ đang test version mới của app nhé', 'Thêm "nhé" cho câu tự nhiên hơn'),
((SELECT sentence_id FROM csl.sentence WHERE content = 'Bạn check email giúp mình nhé'),
 (SELECT user_id FROM csl.app_user WHERE email = 'speaker02@demo.local'), 'accepted', NULL, NULL),
((SELECT sentence_id FROM csl.sentence WHERE content = 'Team mình đã deploy feature lên production'),
 (SELECT user_id FROM csl.app_user WHERE email = 'sonht@codeswitchlabel.local'), 'accepted', NULL, NULL),
((SELECT sentence_id FROM csl.sentence WHERE content = 'Anh ấy share screen để demo sản phẩm'),
 (SELECT user_id FROM csl.app_user WHERE email = 'speaker02@demo.local'), 'accepted', NULL, NULL),
((SELECT sentence_id FROM csl.sentence WHERE content = 'Cô ấy gửi file qua Drive rồi'),
 (SELECT user_id FROM csl.app_user WHERE email = 'speaker02@demo.local'), 'rejected', NULL,
 'Câu không chứa code-switch tiếng Việt–Anh');

-- v1.3: sentence statuses are applied automatically by trg_sentence_status
-- (6 validated, 1 rejected; 'Chúng ta cần review code trước khi merge branch'
--  intentionally left pending_validation)

-- 3 tasks: 2 recording + 1 review
INSERT INTO csl.task (created_by, task_type, title, description, target_qty, deadline, status) VALUES
((SELECT user_id FROM csl.app_user WHERE email = 'baotq@codeswitchlabel.local'),
 'recording', 'Batch 1 - daily-life sentences', 'Record the first batch of validated sentences', 6, now() + interval '7 days',  'in_progress'),
((SELECT user_id FROM csl.app_user WHERE email = 'taskmanager01@demo.local'),
 'recording', 'Batch 2 - workplace sentences',  'Second batch, includes one sentence pending validation', 4, now() + interval '10 days', 'open'),
((SELECT user_id FROM csl.app_user WHERE email = 'baotq@codeswitchlabel.local'),
 'review',    'Review Batch 1',                 'First-pass review of Batch 1 recordings', 6, now() + interval '5 days',  'in_progress');

INSERT INTO csl.task_assignment (task_id, user_id) VALUES
((SELECT task_id FROM csl.task WHERE title = 'Batch 1 - daily-life sentences'), (SELECT user_id FROM csl.app_user WHERE email = 'speaker01@demo.local')),
((SELECT task_id FROM csl.task WHERE title = 'Batch 2 - workplace sentences'),  (SELECT user_id FROM csl.app_user WHERE email = 'sonht@codeswitchlabel.local')),
((SELECT task_id FROM csl.task WHERE title = 'Review Batch 1'),                 (SELECT user_id FROM csl.app_user WHERE email = 'reviewer01@demo.local'));

-- recording-task worklists
INSERT INTO csl.task_sentence (task_id, sentence_id) VALUES
((SELECT task_id FROM csl.task WHERE title = 'Batch 1 - daily-life sentences'), (SELECT sentence_id FROM csl.sentence WHERE content = 'Em nhớ upload tài liệu trước deadline nhé')),
((SELECT task_id FROM csl.task WHERE title = 'Batch 1 - daily-life sentences'), (SELECT sentence_id FROM csl.sentence WHERE content = 'Mình sẽ update report sau meeting nhé')),
((SELECT task_id FROM csl.task WHERE title = 'Batch 1 - daily-life sentences'), (SELECT sentence_id FROM csl.sentence WHERE content = 'Họ đang test version mới của app nhé')),
((SELECT task_id FROM csl.task WHERE title = 'Batch 1 - daily-life sentences'), (SELECT sentence_id FROM csl.sentence WHERE content = 'Bạn check email giúp mình nhé')),
((SELECT task_id FROM csl.task WHERE title = 'Batch 1 - daily-life sentences'), (SELECT sentence_id FROM csl.sentence WHERE content = 'Team mình đã deploy feature lên production')),
((SELECT task_id FROM csl.task WHERE title = 'Batch 1 - daily-life sentences'), (SELECT sentence_id FROM csl.sentence WHERE content = 'Anh ấy share screen để demo sản phẩm')),
((SELECT task_id FROM csl.task WHERE title = 'Batch 2 - workplace sentences'),  (SELECT sentence_id FROM csl.sentence WHERE content = 'Họ đang test version mới của app nhé')),
((SELECT task_id FROM csl.task WHERE title = 'Batch 2 - workplace sentences'),  (SELECT sentence_id FROM csl.sentence WHERE content = 'Team mình đã deploy feature lên production')),
((SELECT task_id FROM csl.task WHERE title = 'Batch 2 - workplace sentences'),  (SELECT sentence_id FROM csl.sentence WHERE content = 'Anh ấy share screen để demo sản phẩm')),
((SELECT task_id FROM csl.task WHERE title = 'Batch 2 - workplace sentences'),  (SELECT sentence_id FROM csl.sentence WHERE content = 'Chúng ta cần review code trước khi merge branch'));

-- 10 recordings: 7 approved, 2 rejected (incl. 1 re-recorded successfully), 1 pending review.
-- demo/0009 and demo/0010 are ad-hoc recordings (no task) — random-review flow.
INSERT INTO csl.recording (sentence_id, speaker_id, task_id, storage_key, duration_sec, audio_format, attempt_no) VALUES
((SELECT sentence_id FROM csl.sentence WHERE content = 'Em nhớ upload tài liệu trước deadline nhé'),  (SELECT user_id FROM csl.app_user WHERE email = 'speaker01@demo.local'), (SELECT task_id FROM csl.task WHERE title = 'Batch 1 - daily-life sentences'), 'demo/0001.wav', 4.20, 'wav', 1),
((SELECT sentence_id FROM csl.sentence WHERE content = 'Mình sẽ update report sau meeting nhé'),      (SELECT user_id FROM csl.app_user WHERE email = 'speaker01@demo.local'), (SELECT task_id FROM csl.task WHERE title = 'Batch 1 - daily-life sentences'), 'demo/0002.wav', 3.80, 'wav', 1),
((SELECT sentence_id FROM csl.sentence WHERE content = 'Họ đang test version mới của app nhé'),       (SELECT user_id FROM csl.app_user WHERE email = 'speaker02@demo.local'), (SELECT task_id FROM csl.task WHERE title = 'Batch 1 - daily-life sentences'), 'demo/0003.wav', 3.10, 'wav', 1),
((SELECT sentence_id FROM csl.sentence WHERE content = 'Họ đang test version mới của app nhé'),       (SELECT user_id FROM csl.app_user WHERE email = 'speaker02@demo.local'), (SELECT task_id FROM csl.task WHERE title = 'Batch 1 - daily-life sentences'), 'demo/0004.wav', 3.05, 'wav', 2),
((SELECT sentence_id FROM csl.sentence WHERE content = 'Bạn check email giúp mình nhé'),               (SELECT user_id FROM csl.app_user WHERE email = 'speaker02@demo.local'), (SELECT task_id FROM csl.task WHERE title = 'Batch 1 - daily-life sentences'), 'demo/0005.wav', 2.90, 'wav', 1),
((SELECT sentence_id FROM csl.sentence WHERE content = 'Team mình đã deploy feature lên production'), (SELECT user_id FROM csl.app_user WHERE email = 'sonht@codeswitchlabel.local'), (SELECT task_id FROM csl.task WHERE title = 'Batch 2 - workplace sentences'), 'demo/0006.wav', 4.60, 'wav', 1),
((SELECT sentence_id FROM csl.sentence WHERE content = 'Anh ấy share screen để demo sản phẩm'),       (SELECT user_id FROM csl.app_user WHERE email = 'sonht@codeswitchlabel.local'), (SELECT task_id FROM csl.task WHERE title = 'Batch 2 - workplace sentences'), 'demo/0007.wav', 3.40, 'wav', 1),
((SELECT sentence_id FROM csl.sentence WHERE content = 'Anh ấy share screen để demo sản phẩm'),       (SELECT user_id FROM csl.app_user WHERE email = 'sonht@codeswitchlabel.local'), (SELECT task_id FROM csl.task WHERE title = 'Batch 2 - workplace sentences'), 'demo/0008.wav', 3.55, 'wav', 2),
((SELECT sentence_id FROM csl.sentence WHERE content = 'Em nhớ upload tài liệu trước deadline nhé'),  (SELECT user_id FROM csl.app_user WHERE email = 'speaker03@demo.local'), NULL, 'demo/0009.wav', 4.05, 'wav', 1),
((SELECT sentence_id FROM csl.sentence WHERE content = 'Team mình đã deploy feature lên production'), (SELECT user_id FROM csl.app_user WHERE email = 'speaker04@demo.local'), NULL, 'demo/0010.wav', 4.85, 'wav', 1);

-- v1.3: 16 reviews = 9 first-pass (7 approved, 2 rejected with reasons)
--        + 7 second-pass approvals; task_id NULL = random spot-check / stage-2 pass
INSERT INTO csl.review (recording_id, reviewer_id, task_id, stage, decision, comment) VALUES
((SELECT recording_id FROM csl.recording WHERE storage_key = 'demo/0001.wav'), (SELECT user_id FROM csl.app_user WHERE email = 'reviewer01@demo.local'), (SELECT task_id FROM csl.task WHERE title = 'Review Batch 1'), 1, 'approved', 'Rõ ràng, tự nhiên'),
((SELECT recording_id FROM csl.recording WHERE storage_key = 'demo/0002.wav'), (SELECT user_id FROM csl.app_user WHERE email = 'reviewer01@demo.local'), NULL, 1, 'approved', 'OK'),
((SELECT recording_id FROM csl.recording WHERE storage_key = 'demo/0003.wav'), (SELECT user_id FROM csl.app_user WHERE email = 'reviewer01@demo.local'), (SELECT task_id FROM csl.task WHERE title = 'Review Batch 1'), 1, 'rejected', 'Ồn nền, âm lượng nhỏ'),
((SELECT recording_id FROM csl.recording WHERE storage_key = 'demo/0004.wav'), (SELECT user_id FROM csl.app_user WHERE email = 'reviewer01@demo.local'), (SELECT task_id FROM csl.task WHERE title = 'Review Batch 1'), 1, 'approved', 'Bản thu lại đạt yêu cầu'),
((SELECT recording_id FROM csl.recording WHERE storage_key = 'demo/0005.wav'), (SELECT user_id FROM csl.app_user WHERE email = 'thangpd@codeswitchlabel.local'),  NULL, 1, 'approved', 'OK'),
((SELECT recording_id FROM csl.recording WHERE storage_key = 'demo/0006.wav'), (SELECT user_id FROM csl.app_user WHERE email = 'thangpd@codeswitchlabel.local'),  NULL, 1, 'approved', 'Phát âm tốt'),
((SELECT recording_id FROM csl.recording WHERE storage_key = 'demo/0007.wav'), (SELECT user_id FROM csl.app_user WHERE email = 'minhv@codeswitchlabel.local'), NULL, 1, 'rejected', 'Phát âm từ "production" chưa chuẩn'),
((SELECT recording_id FROM csl.recording WHERE storage_key = 'demo/0009.wav'), (SELECT user_id FROM csl.app_user WHERE email = 'reviewer02@demo.local'), NULL, 1, 'approved', 'OK'),
((SELECT recording_id FROM csl.recording WHERE storage_key = 'demo/0010.wav'), (SELECT user_id FROM csl.app_user WHERE email = 'reviewer02@demo.local'), NULL, 1, 'approved', 'OK');

INSERT INTO csl.review_rejection_reason (review_id, reason_id) VALUES
((SELECT review_id FROM csl.review WHERE comment = 'Ồn nền, âm lượng nhỏ'),             (SELECT reason_id FROM csl.rejection_reason WHERE reason_code = 'BACKGROUND_NOISE')),
((SELECT review_id FROM csl.review WHERE comment = 'Ồn nền, âm lượng nhỏ'),             (SELECT reason_id FROM csl.rejection_reason WHERE reason_code = 'LOW_VOLUME')),
((SELECT review_id FROM csl.review WHERE comment = 'Phát âm từ "production" chưa chuẩn'), (SELECT reason_id FROM csl.rejection_reason WHERE reason_code = 'MISPRONUNCIATION'));

-- v1.3: second-pass approvals (review.stages = 2). trg_recording_status promotes
-- each recording to 'approved' only when every stage has an approval.
INSERT INTO csl.review (recording_id, reviewer_id, task_id, stage, decision, comment) VALUES
((SELECT recording_id FROM csl.recording WHERE storage_key = 'demo/0001.wav'), (SELECT user_id FROM csl.app_user WHERE email = 'reviewer02@demo.local'),       NULL, 2, 'approved', 'Giai đoạn 2: đạt'),
((SELECT recording_id FROM csl.recording WHERE storage_key = 'demo/0002.wav'), (SELECT user_id FROM csl.app_user WHERE email = 'minhv@codeswitchlabel.local'),  NULL, 2, 'approved', 'Giai đoạn 2: đạt'),
((SELECT recording_id FROM csl.recording WHERE storage_key = 'demo/0004.wav'), (SELECT user_id FROM csl.app_user WHERE email = 'thangpd@codeswitchlabel.local'), NULL, 2, 'approved', 'Bản thu lại ổn ở giai đoạn 2'),
((SELECT recording_id FROM csl.recording WHERE storage_key = 'demo/0005.wav'), (SELECT user_id FROM csl.app_user WHERE email = 'reviewer01@demo.local'),      NULL, 2, 'approved', 'Giai đoạn 2: đạt'),
((SELECT recording_id FROM csl.recording WHERE storage_key = 'demo/0006.wav'), (SELECT user_id FROM csl.app_user WHERE email = 'reviewer01@demo.local'),      NULL, 2, 'approved', 'Giai đoạn 2: đạt'),
((SELECT recording_id FROM csl.recording WHERE storage_key = 'demo/0009.wav'), (SELECT user_id FROM csl.app_user WHERE email = 'minhv@codeswitchlabel.local'),  NULL, 2, 'approved', 'Giai đoạn 2: đạt'),
((SELECT recording_id FROM csl.recording WHERE storage_key = 'demo/0010.wav'), (SELECT user_id FROM csl.app_user WHERE email = 'thangpd@codeswitchlabel.local'), NULL, 2, 'approved', 'Giai đoạn 2: đạt');

-- v1.3: recording statuses are applied automatically by trg_recording_status
-- (demo/0008.wav stays pending_review — visible in the review queue)

-- review-task worklist
INSERT INTO csl.task_recording (task_id, recording_id) VALUES
((SELECT task_id FROM csl.task WHERE title = 'Review Batch 1'), (SELECT recording_id FROM csl.recording WHERE storage_key = 'demo/0001.wav')),
((SELECT task_id FROM csl.task WHERE title = 'Review Batch 1'), (SELECT recording_id FROM csl.recording WHERE storage_key = 'demo/0002.wav')),
((SELECT task_id FROM csl.task WHERE title = 'Review Batch 1'), (SELECT recording_id FROM csl.recording WHERE storage_key = 'demo/0003.wav')),
((SELECT task_id FROM csl.task WHERE title = 'Review Batch 1'), (SELECT recording_id FROM csl.recording WHERE storage_key = 'demo/0004.wav')),
((SELECT task_id FROM csl.task WHERE title = 'Review Batch 1'), (SELECT recording_id FROM csl.recording WHERE storage_key = 'demo/0005.wav'));

-- 2 datasets: 1 draft + 1 released
INSERT INTO csl.dataset (dataset_name, version, description, status, released_by, released_at) VALUES
('CSL VN-EN Demo', '0.1', 'Draft demo dataset from Batch 1', 'draft', NULL, NULL),
('CodeSwitch Corpus', '1.0', 'First released corpus slice', 'released',
 (SELECT user_id FROM csl.app_user WHERE email = 'diepln@codeswitchlabel.local'), now() - interval '2 days');

INSERT INTO csl.dataset_recording (dataset_id, recording_id) VALUES
((SELECT dataset_id FROM csl.dataset WHERE dataset_name = 'CSL VN-EN Demo'),  (SELECT recording_id FROM csl.recording WHERE storage_key = 'demo/0001.wav')),
((SELECT dataset_id FROM csl.dataset WHERE dataset_name = 'CSL VN-EN Demo'),  (SELECT recording_id FROM csl.recording WHERE storage_key = 'demo/0002.wav')),
((SELECT dataset_id FROM csl.dataset WHERE dataset_name = 'CSL VN-EN Demo'),  (SELECT recording_id FROM csl.recording WHERE storage_key = 'demo/0004.wav')),
((SELECT dataset_id FROM csl.dataset WHERE dataset_name = 'CodeSwitch Corpus'), (SELECT recording_id FROM csl.recording WHERE storage_key = 'demo/0005.wav')),
((SELECT dataset_id FROM csl.dataset WHERE dataset_name = 'CodeSwitch Corpus'), (SELECT recording_id FROM csl.recording WHERE storage_key = 'demo/0006.wav')),
((SELECT dataset_id FROM csl.dataset WHERE dataset_name = 'CodeSwitch Corpus'), (SELECT recording_id FROM csl.recording WHERE storage_key = 'demo/0009.wav')),
((SELECT dataset_id FROM csl.dataset WHERE dataset_name = 'CodeSwitch Corpus'), (SELECT recording_id FROM csl.recording WHERE storage_key = 'demo/0010.wav'));

COMMIT;

-- ================================================================= 8. smoke test
-- Positive: full workflow via the v1.3 auto-status triggers.
-- Negative: 6 guards that MUST fire. Everything is rolled back at the end.
SET search_path TO csl, public;
BEGIN;

INSERT INTO csl.app_user (role_id, full_name, email, password_hash)
SELECT role_id, 'Smoke Speaker', 'smoke.speaker@local', 'x' FROM csl.role WHERE role_name = 'speaker';
INSERT INTO csl.app_user (role_id, full_name, email, password_hash)
SELECT role_id, 'Smoke Reviewer', 'smoke.reviewer@local', 'x' FROM csl.role WHERE role_name = 'reviewer';

INSERT INTO csl.sentence (content, source, has_code_switch, created_by)
VALUES ('SMOKE: em nhớ upload file trước deadline nhé', 'speaker_contribution', true,
        (SELECT user_id FROM csl.app_user WHERE email = 'smoke.speaker@local'));

-- v1.3: this validation auto-flips the sentence to 'validated' — no manual UPDATE
INSERT INTO csl.text_validation (sentence_id, validator_id, validation_result)
VALUES ((SELECT sentence_id FROM csl.sentence WHERE content = 'SMOKE: em nhớ upload file trước deadline nhé'),
        (SELECT user_id FROM csl.app_user WHERE email = 'smoke.speaker@local'),
        'accepted');

INSERT INTO csl.task (created_by, task_type, title, target_qty, deadline)
VALUES ((SELECT user_id FROM csl.app_user WHERE email = 'admin@codeswitchlabel.local'),
        'recording', 'Smoke recording task', 5, now() + interval '7 days');

INSERT INTO csl.task_assignment (task_id, user_id)
VALUES ((SELECT task_id FROM csl.task WHERE title = 'Smoke recording task'),
        (SELECT user_id FROM csl.app_user WHERE email = 'smoke.speaker@local'));

INSERT INTO csl.task_sentence (task_id, sentence_id)
VALUES ((SELECT task_id FROM csl.task WHERE title = 'Smoke recording task'),
        (SELECT sentence_id FROM csl.sentence WHERE content = 'SMOKE: em nhớ upload file trước deadline nhé'));

-- succeeds ONLY because the validation above auto-validated the sentence
INSERT INTO csl.recording (sentence_id, speaker_id, task_id, storage_key, duration_sec, audio_format, attempt_no)
VALUES ((SELECT sentence_id FROM csl.sentence WHERE content = 'SMOKE: em nhớ upload file trước deadline nhé'),
        (SELECT user_id FROM csl.app_user WHERE email = 'smoke.speaker@local'),
        (SELECT task_id FROM csl.task WHERE title = 'Smoke recording task'),
        'smoke/01.wav', 3.20, 'wav', 1);

INSERT INTO csl.task (created_by, task_type, title, target_qty, deadline)
VALUES ((SELECT user_id FROM csl.app_user WHERE email = 'admin@codeswitchlabel.local'),
        'review', 'Smoke review task', 5, now() + interval '7 days');

INSERT INTO csl.task_assignment (task_id, user_id)
VALUES ((SELECT task_id FROM csl.task WHERE title = 'Smoke review task'),
        (SELECT user_id FROM csl.app_user WHERE email = 'smoke.reviewer@local'));

INSERT INTO csl.task_recording (task_id, recording_id)
VALUES ((SELECT task_id FROM csl.task WHERE title = 'Smoke review task'),
        (SELECT recording_id FROM csl.recording WHERE storage_key = 'smoke/01.wav'));

-- v1.3: two-stage approval (review.stages = 2). Stage 1 alone does NOT approve
-- the recording; the stage-2 approval triggers the promotion.
INSERT INTO csl.review (recording_id, reviewer_id, task_id, stage, decision, comment) VALUES
((SELECT recording_id FROM csl.recording WHERE storage_key = 'smoke/01.wav'),
 (SELECT user_id FROM csl.app_user WHERE email = 'smoke.reviewer@local'),
 (SELECT task_id FROM csl.task WHERE title = 'Smoke review task'), 1, 'approved', 'smoke stage 1'),
((SELECT recording_id FROM csl.recording WHERE storage_key = 'smoke/01.wav'),
 (SELECT user_id FROM csl.app_user WHERE email = 'smoke.reviewer@local'),
 NULL, 2, 'approved', 'smoke stage 2');

-- dataset inclusion succeeds ONLY because the auto-approval set status = 'approved'
INSERT INTO csl.dataset (dataset_name, version, description)
VALUES ('smoke-dataset', 'v0', 'SMOKE TEST — rolled back');

INSERT INTO csl.dataset_recording (dataset_id, recording_id)
VALUES ((SELECT dataset_id FROM csl.dataset WHERE dataset_name = 'smoke-dataset'),
        (SELECT recording_id FROM csl.recording WHERE storage_key = 'smoke/01.wav'));

-- ------------------- negative tests: every guard below MUST fire -------------
-- Pattern: if the guard does NOT fire, the marker raises errcode 'GUARD',
-- which is NOT caught here, so the script fails loudly instead of passing silently.
DO $$ DECLARE
  v_rec bigint; v_speaker bigint; v_rev bigint; v_sent bigint; v_pending bigint;
BEGIN
  SELECT r.recording_id, r.speaker_id, r.sentence_id INTO v_rec, v_speaker, v_sent
  FROM csl.recording r WHERE r.storage_key = 'smoke/01.wav';
  SELECT user_id INTO v_rev FROM csl.app_user WHERE email = 'smoke.reviewer@local';

  -- 1. reviewer cannot be the speaker
  BEGIN
    INSERT INTO csl.review (recording_id, reviewer_id, stage, decision)
    VALUES (v_rec, v_speaker, 9, 'approved');
    RAISE EXCEPTION 'self-review guard did not fire' USING ERRCODE = 'GUARD';
  EXCEPTION WHEN raise_exception THEN NULL; END;
  RAISE NOTICE 'guard 1/6: self-review blocked';

  -- 2. attempt_no unique per (sentence, speaker)
  BEGIN
    INSERT INTO csl.recording (sentence_id, speaker_id, storage_key, duration_sec, attempt_no)
    VALUES (v_sent, v_speaker, 'smoke/02.wav', 3.00, 1);
    RAISE EXCEPTION 'unique attempt_no guard did not fire' USING ERRCODE = 'GUARD';
  EXCEPTION WHEN unique_violation THEN NULL; END;
  RAISE NOTICE 'guard 2/6: duplicate attempt_no blocked';

  -- 3. rejection reasons only on rejected reviews
  BEGIN
    INSERT INTO csl.review_rejection_reason (review_id, reason_id)
    VALUES ((SELECT review_id FROM csl.review WHERE comment = 'smoke stage 1'),
            (SELECT reason_id FROM csl.rejection_reason WHERE reason_code = 'BACKGROUND_NOISE'));
    RAISE EXCEPTION 'reason-on-approved guard did not fire' USING ERRCODE = 'GUARD';
  EXCEPTION WHEN raise_exception THEN NULL; END;
  RAISE NOTICE 'guard 3/6: reason on approved review blocked';

  -- 4. rejected reviews must cite at least one rejection reason (deferred constraint)
  BEGIN
    INSERT INTO csl.review (recording_id, reviewer_id, stage, decision)
    VALUES (v_rec, v_rev, 3, 'rejected');
    EXECUTE 'SET CONSTRAINTS ALL IMMEDIATE';  -- flush deferred triggers now
    RAISE EXCEPTION 'rejection-reason guard did not fire' USING ERRCODE = 'GUARD';
  EXCEPTION WHEN raise_exception THEN NULL; END;
  RAISE NOTICE 'guard 4/6: rejection without reason blocked';

  -- 5. a pending (un-validated) sentence cannot be recorded
  INSERT INTO csl.sentence (content, source, has_code_switch, created_by)
  VALUES ('SMOKE: câu này chưa được validate', 'speaker_contribution', true, v_speaker)
  RETURNING sentence_id INTO v_pending;
  BEGIN
    INSERT INTO csl.recording (sentence_id, speaker_id, storage_key, duration_sec, attempt_no)
    VALUES (v_pending, v_speaker, 'smoke/03.wav', 2.00, 1);
    RAISE EXCEPTION 'sentence-validated guard did not fire' USING ERRCODE = 'GUARD';
  EXCEPTION WHEN raise_exception THEN NULL; END;
  RAISE NOTICE 'guard 5/6: recording of pending sentence blocked';

  -- 6. only approved recordings may enter a dataset
  INSERT INTO csl.recording (sentence_id, speaker_id, storage_key, duration_sec, attempt_no)
  VALUES (v_sent, v_speaker, 'smoke/04.wav', 2.50, 2);  -- stays pending_review
  BEGIN
    INSERT INTO csl.dataset_recording (dataset_id, recording_id)
    VALUES ((SELECT dataset_id FROM csl.dataset WHERE dataset_name = 'smoke-dataset'),
            (SELECT recording_id FROM csl.recording WHERE storage_key = 'smoke/04.wav'));
    RAISE EXCEPTION 'dataset-approved guard did not fire' USING ERRCODE = 'GUARD';
  EXCEPTION WHEN raise_exception THEN NULL; END;
  RAISE NOTICE 'guard 6/6: pending recording in dataset blocked';
END $$;

ROLLBACK;  -- smoke data fully discarded; schema + accounts + demo data remain

-- ================================================= 9. post-install verification
-- All queries below run against the PERSISTENT demo data
SELECT u.email, u.full_name, r.role_name::text AS role, u.status::text AS status
FROM csl.app_user u JOIN csl.role r ON r.role_id = u.role_id
ORDER BY r.role_name, u.email;

-- v1.3: confirm auto-status produced the expected demo states
-- expected: 7 approved, 2 rejected, 1 pending_review
SELECT storage_key, status::text AS status, attempt_no
FROM csl.recording ORDER BY storage_key;

SELECT * FROM csl.v_dashboard_summary;
SELECT * FROM csl.v_task_progress;
SELECT * FROM csl.v_speaker_performance;
SELECT * FROM csl.v_top_rejection_reasons;

SELECT 'CodeSwitchLabel v1.3 installed: 16 tables, 13 accounts, demo corpus, smoke tests passed' AS result;