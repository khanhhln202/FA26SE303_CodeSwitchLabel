-- ============================================================
-- V16 — Audit trail for recording, review, dataset, script_proposal, app_user
--
-- Registration NFR: "retain complete histories of recordings, reviews and
-- data changes". Before V16 only script, campaign and task were audited.
-- The baseline schema (docs/codeswitchlabel.sql) already has these triggers;
-- this migration brings databases created earlier forward.
--
-- fn_audit now strips 'password_hash' from the old/new JSON so that
-- auditing app_user never copies bcrypt hashes into audit_log.
-- ============================================================

BEGIN;

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
        CASE WHEN TG_OP = 'INSERT' THEN NULL ELSE to_jsonb(OLD) - 'password_hash' END,
        CASE WHEN TG_OP = 'DELETE' THEN NULL ELSE to_jsonb(NEW) - 'password_hash' END
    );
    RETURN NULL;   -- AFTER trigger
END $$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_audit_recording ON recording;
CREATE TRIGGER trg_audit_recording
    AFTER INSERT OR UPDATE OR DELETE ON recording
    FOR EACH ROW EXECUTE FUNCTION fn_audit('recording', 'recording_id');

DROP TRIGGER IF EXISTS trg_audit_review ON review;
CREATE TRIGGER trg_audit_review
    AFTER INSERT OR UPDATE OR DELETE ON review
    FOR EACH ROW EXECUTE FUNCTION fn_audit('review', 'review_id');

DROP TRIGGER IF EXISTS trg_audit_dataset ON dataset;
CREATE TRIGGER trg_audit_dataset
    AFTER INSERT OR UPDATE OR DELETE ON dataset
    FOR EACH ROW EXECUTE FUNCTION fn_audit('dataset', 'dataset_id');

DROP TRIGGER IF EXISTS trg_audit_script_proposal ON script_proposal;
CREATE TRIGGER trg_audit_script_proposal
    AFTER INSERT OR UPDATE OR DELETE ON script_proposal
    FOR EACH ROW EXECUTE FUNCTION fn_audit('script_proposal', 'proposal_id');

DROP TRIGGER IF EXISTS trg_audit_app_user ON app_user;
CREATE TRIGGER trg_audit_app_user
    AFTER INSERT OR UPDATE OR DELETE ON app_user
    FOR EACH ROW EXECUTE FUNCTION fn_audit('app_user', 'user_id');

COMMIT;
