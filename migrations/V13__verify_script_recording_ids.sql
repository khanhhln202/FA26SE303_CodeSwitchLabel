-- ============================================================
-- V13 verification (psql): ID 12 chữ số và 13 bộ đếm tuần tự.
--
-- Chạy trong transaction ROLLBACK ở cuối nên không để lại dữ liệu.
-- Lưu ý: sequence KHÔNG rollback, nên mỗi lần chạy verify sẽ tăng bộ đếm;
-- điều này vô hại vì ID chỉ cần duy nhất và tăng dần.
-- Run: psql -d codeswitchlabel -v ON_ERROR_STOP=1 -f migrations/V13__verify_script_recording_ids.sql
-- ============================================================
BEGIN;

DO $$
DECLARE
    v_len INT;
BEGIN
    SELECT character_maximum_length INTO v_len
      FROM information_schema.columns
     WHERE table_name = 'script' AND column_name = 'script_id';
    IF v_len < 16 THEN
        RAISE EXCEPTION 'script.script_id quá hẹp: %', v_len;
    END IF;

    SELECT character_maximum_length INTO v_len
      FROM information_schema.columns
     WHERE table_name = 'recording' AND column_name = 'recording_id';
    IF v_len < 32 THEN
        RAISE EXCEPTION 'recording.recording_id quá hẹp: %', v_len;
    END IF;

    SELECT character_maximum_length INTO v_len
      FROM information_schema.columns
     WHERE table_name = 'audit_log' AND column_name = 'entity_id';
    IF v_len < 32 THEN
        RAISE EXCEPTION 'audit_log.entity_id quá hẹp: %', v_len;
    END IF;

    RAISE NOTICE 'OK — các cột ID đã đủ rộng.';
END $$;

DO $$
DECLARE
    v_a TEXT;
    v_b TEXT;
    v_fallback TEXT;
    v_legacy_a TEXT;
    v_legacy_b TEXT;
BEGIN
    v_a := fn_generate_script_id(1, 'it_technology', 1);
    v_b := fn_generate_script_id(1, 'it_technology', 1);
    v_fallback := fn_generate_script_id(3, 'education', 2);

    IF v_a !~ '^s_[0-9]{12}$' THEN
        RAISE EXCEPTION 'script_id mới sai định dạng: %', v_a;
    END IF;
    IF left(v_a, 5) <> 's_111' THEN
        RAISE EXCEPTION 'script_id mới sai 3 chữ số nghĩa: %', v_a;
    END IF;
    IF v_b !~ '^s_[0-9]{12}$' OR v_b = v_a THEN
        RAISE EXCEPTION 'serial không tăng/không duy nhất: %, %', v_a, v_b;
    END IF;
    IF v_fallback !~ '^s_322[0-9]{9}$' THEN
        RAISE EXCEPTION 'fallback >2 từ sai định dạng: %', v_fallback;
    END IF;

    -- ID cũ 9 chữ số vẫn tồn tại và tạo recording tương ứng được.
    BEGIN
        INSERT INTO script (script_id, cs_content, vi_content, status, word_count, en_word_count, domain, created_by, created_at, updated_at)
        VALUES ('s_111000001', '[vi]A [en]test [vi]B', '[vi]A test B', 'pending_validation', 3, 1, 'it_technology', 1, now(), now());
        INSERT INTO script_word (script_id, word_position, en_word, vi_word, relation)
        VALUES ('s_111000001', 1, 'test', 'thử', 'semantic_equivalent');
        INSERT INTO recording (recording_id, sentence_variant, script_id, speaker_id, cloud_link, duration_sec, status, created_at, updated_at)
        VALUES ('r_cs_111000001', 'code_switching', 's_111000001', 1, 's3://legacy/verify.wav', 1.0, 'pending_review', now(), now());
    EXCEPTION WHEN unique_violation THEN NULL; END;

    IF NOT EXISTS (SELECT 1 FROM script WHERE script_id = 's_111000001') THEN
        RAISE EXCEPTION 'legacy script_id s_111000001 không tồn tại';
    END IF;
    IF NOT EXISTS (SELECT 1 FROM recording WHERE recording_id = 'r_cs_111000001') THEN
        RAISE EXCEPTION 'legacy recording_id r_cs_111000001 không tồn tại';
    END IF;

    v_legacy_a := fn_generate_recording_id('s_111000001', 'code_switching', 1);
    v_legacy_b := fn_generate_recording_id('s_111000001', 'code_switching', 2);

    IF v_legacy_a <> 'r_cs_111000001' THEN
        RAISE EXCEPTION 'recording legacy sai: %', v_legacy_a;
    END IF;
    IF v_legacy_b <> 'r_cs_111000001_t2' THEN
        RAISE EXCEPTION 'recording legacy retake sai: %', v_legacy_b;
    END IF;

    IF fn_generate_recording_id(v_a, 'pure_vietnamese', 10) <> 'r_vi_' || substr(v_a, 3) || '_t10' THEN
        RAISE EXCEPTION 'recording ID mới sai';
    END IF;

    RAISE NOTICE 'OK — script_id %, fallback %, recording legacy %.', v_a, v_fallback, v_legacy_b;
END $$;

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_script_id_format') THEN
        RAISE EXCEPTION 'thiếu ck_script_id_format';
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_recording_id_format') THEN
        RAISE EXCEPTION 'thiếu ck_recording_id_format';
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_recording_script_serial') THEN
        RAISE EXCEPTION 'thiếu ck_recording_script_serial';
    END IF;

    IF (SELECT COUNT(*) FROM pg_class
         WHERE relkind = 'S'
           AND relname IN (
               'script_serial_1_1_1','script_serial_1_1_2',
               'script_serial_1_2_1','script_serial_1_2_2',
               'script_serial_1_3_1','script_serial_1_3_2',
               'script_serial_2_1_1','script_serial_2_1_2',
               'script_serial_2_2_1','script_serial_2_2_2',
               'script_serial_2_3_1','script_serial_2_3_2',
               'script_serial_other')) <> 13 THEN
        RAISE EXCEPTION 'không đủ 13 bộ đếm script_id';
    END IF;

    RAISE NOTICE 'OK — constraint và 13 bộ đếm đã có.';
END $$;

ROLLBACK;
