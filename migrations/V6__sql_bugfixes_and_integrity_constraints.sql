-- ============================================================
-- V6 — SQL Bugfixes, Integrity Constraints & Domain Hardening (TEAM_007)
-- PostgreSQL 13+. Idempotent where possible, single transaction.
-- Run: psql -d codeswitchlabel -f migrations/V6__sql_bugfixes_and_integrity_constraints.sql
-- ============================================================

BEGIN;

-- ------------------------------------------------------------
-- 1. Takes 10-19 allowed by recording_id regex
-- ------------------------------------------------------------
ALTER TABLE recording DROP CONSTRAINT IF EXISTS ck_recording_id_format;
ALTER TABLE recording ADD CONSTRAINT ck_recording_id_format
    CHECK (recording_id ~ '^r_(cs|vi)_[0-9]{9}(_t([2-9]|[1-9][0-9]+))?$');

ALTER TABLE recording DROP CONSTRAINT IF EXISTS ck_recording_script_serial;
ALTER TABLE recording ADD CONSTRAINT ck_recording_script_serial
    CHECK (regexp_replace(recording_id, '^r_(cs|vi)_([0-9]{9})(_t([2-9]|[1-9][0-9]+))?$', '\2') = substr(script_id, 3));

-- ------------------------------------------------------------
-- 2. Campaign date checks anchored to 'Asia/Ho_Chi_Minh'
-- ------------------------------------------------------------
CREATE OR REPLACE FUNCTION fn_validate_task_campaign_window() RETURNS trigger AS $$
DECLARE
    v_start_date DATE;
    v_end_date   DATE;
BEGIN
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

-- ------------------------------------------------------------
-- 3. Script re-validation requires review after reset
-- ------------------------------------------------------------
CREATE OR REPLACE FUNCTION fn_script_validated_by_domain_reviewer() RETURNS trigger AS $$
BEGIN
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

-- ------------------------------------------------------------
-- 4. fn_recording_single_speaker advisory lock against races
-- ------------------------------------------------------------
CREATE OR REPLACE FUNCTION fn_recording_single_speaker() RETURNS trigger AS $$
BEGIN
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

-- ------------------------------------------------------------
-- 5 & 9. Review rounds dynamic config + Majority-driven script content reset
-- ------------------------------------------------------------
-- Drop per-reviewer content reset trigger: wait for majority outcome
DROP TRIGGER IF EXISTS trg_rr_content_resets_script ON review_rejection_reason;
DROP FUNCTION IF EXISTS fn_review_content_resets_script();

ALTER TABLE review DROP CONSTRAINT IF EXISTS review_review_round_check;
ALTER TABLE review DROP CONSTRAINT IF EXISTS ck_review_round_positive;
ALTER TABLE review ADD CONSTRAINT ck_review_round_positive CHECK (review_round >= 1);

CREATE OR REPLACE FUNCTION fn_review_majority_status() RETURNS trigger AS $$
DECLARE
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
            END IF;
        END IF;
    END IF;

    RETURN NEW;
END $$ LANGUAGE plpgsql;

-- ------------------------------------------------------------
-- 6. Task-Script domain & status enforcement
-- ------------------------------------------------------------
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

DROP TRIGGER IF EXISTS trg_task_script_validation ON task_script;
CREATE TRIGGER trg_task_script_validation
    BEFORE INSERT OR UPDATE OF task_id, script_id ON task_script
    FOR EACH ROW EXECUTE FUNCTION fn_validate_task_script();

-- ------------------------------------------------------------
-- 7. Task Assignment role enforcement
-- ------------------------------------------------------------
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

DROP TRIGGER IF EXISTS trg_task_assignment_role ON task_assignment;
CREATE TRIGGER trg_task_assignment_role
    BEFORE INSERT OR UPDATE OF task_id, user_id ON task_assignment
    FOR EACH ROW EXECUTE FUNCTION fn_validate_task_assignment_role();

-- ------------------------------------------------------------
-- 8. script_review rejections require error_reason_id
-- ------------------------------------------------------------
ALTER TABLE script_review DROP CONSTRAINT IF EXISTS ck_script_review_rejection_reason;
ALTER TABLE script_review ADD CONSTRAINT ck_script_review_rejection_reason
    CHECK (action <> 'rejected' OR error_reason_id IS NOT NULL);

-- ------------------------------------------------------------
-- 10. Released datasets immutability in dataset_recording
-- ------------------------------------------------------------
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

DROP TRIGGER IF EXISTS trg_dataset_recording_immutable ON dataset_recording;
CREATE TRIGGER trg_dataset_recording_immutable
    BEFORE INSERT OR UPDATE OR DELETE ON dataset_recording
    FOR EACH ROW EXECUTE FUNCTION fn_validate_dataset_recording_modification();

-- ------------------------------------------------------------
-- 11. v_speaker_performance approval rate denominator
-- ------------------------------------------------------------
CREATE OR REPLACE VIEW v_speaker_performance AS
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

-- ------------------------------------------------------------
-- 12. Drop redundant index uq_recording_approved_pair
-- ------------------------------------------------------------
DROP INDEX IF EXISTS uq_recording_approved_pair;

-- ------------------------------------------------------------
-- 13. Duplicate-script guard
-- ------------------------------------------------------------
CREATE UNIQUE INDEX IF NOT EXISTS uq_script_cs_content_md5 ON script (md5(cs_content));

-- ------------------------------------------------------------
-- 14. Drop default 'it_technology' on campaign and task domain
-- ------------------------------------------------------------
ALTER TABLE campaign ALTER COLUMN domain DROP DEFAULT;
ALTER TABLE task ALTER COLUMN domain DROP DEFAULT;

COMMIT;
