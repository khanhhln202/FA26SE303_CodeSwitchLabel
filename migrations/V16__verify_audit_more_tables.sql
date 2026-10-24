-- ============================================================
-- V16 verification (psql): audit trail covers recording, review, dataset,
-- script_proposal, app_user; password_hash never lands in audit_log.
--
-- Runs in a transaction that ROLLBACKs, so no data is left behind.
-- Run: psql -d codeswitchlabel -v ON_ERROR_STOP=1 -f migrations/V16__verify_audit_more_tables.sql
-- ============================================================
BEGIN;

-- 1. All five triggers exist on the right tables.
DO $$
DECLARE
    v_pair TEXT[];
    v_pairs TEXT[][] := ARRAY[
        ['recording','trg_audit_recording'],
        ['review','trg_audit_review'],
        ['dataset','trg_audit_dataset'],
        ['script_proposal','trg_audit_script_proposal'],
        ['app_user','trg_audit_app_user']];
BEGIN
    FOR i IN 1..array_length(v_pairs, 1) LOOP
        IF NOT EXISTS (SELECT 1 FROM pg_trigger tg JOIN pg_class c ON c.oid = tg.tgrelid
                        WHERE c.relname = v_pairs[i][1] AND tg.tgname = v_pairs[i][2]) THEN
            RAISE EXCEPTION 'missing trigger % on %', v_pairs[i][2], v_pairs[i][1];
        END IF;
    END LOOP;
    RAISE NOTICE 'OK  five audit triggers present';
END $$;

-- 2. Behaviour: a new user writes an audit row, and the row has no password_hash.
DO $$
DECLARE
    v_user   BIGINT;
    v_new    JSONB;
    v_count  INT;
BEGIN
    INSERT INTO app_user (role_id, full_name, email, password_hash)
    SELECT role_id, 'V16 verify', 'v16.verify@codeswitchlabel.local', repeat('x', 60)
      FROM role WHERE role_name = 'speaker'
    RETURNING user_id INTO v_user;

    SELECT COUNT(*), MAX(new_value::text)::jsonb
      INTO v_count, v_new
      FROM audit_log
     WHERE entity_type = 'app_user' AND entity_id = v_user::text AND action = 'create';

    IF v_count <> 1 THEN
        RAISE EXCEPTION 'V16 verify: expected 1 audit row for new app_user, got %', v_count;
    END IF;

    IF v_new ? 'password_hash' THEN
        RAISE EXCEPTION 'V16 verify: password_hash leaked into audit_log';
    END IF;

    UPDATE app_user SET status = 'inactive' WHERE user_id = v_user;

    SELECT COUNT(*) INTO v_count
      FROM audit_log
     WHERE entity_type = 'app_user' AND entity_id = v_user::text AND action = 'update';

    IF v_count <> 1 THEN
        RAISE EXCEPTION 'V16 verify: expected 1 update audit row, got %', v_count;
    END IF;

    RAISE NOTICE 'OK  app_user changes audited, password_hash stripped';
END $$;

ROLLBACK;
