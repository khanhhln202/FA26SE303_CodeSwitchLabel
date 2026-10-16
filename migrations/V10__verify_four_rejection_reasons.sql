-- ============================================================
-- V10 verification (psql): lý do từ chối bản ghi còn đúng 4 lý do (TEAM_016).
--
-- Chạy trong transaction mà ROLLBACK ở cuối nên không để lại dữ liệu nào.
-- Run: psql -d codeswitchlabel -v ON_ERROR_STOP=1 -f migrations/V10__verify_four_rejection_reasons.sql
-- ============================================================
BEGIN;

-- ------------------------------------------------------------------
-- 1. Đúng 4 mã đang bật, đúng tên; mỗi nhóm đúng một mã.
-- ------------------------------------------------------------------
DO $$
DECLARE
    v_active TEXT[];
    v_bad    RECORD;
BEGIN
    SELECT array_agg(reason_code ORDER BY reason_code) INTO v_active
      FROM rejection_reason WHERE is_active;

    IF v_active IS DISTINCT FROM ARRAY['background_noise','content_mismatch',
                                       'mispronunciation','other']::TEXT[] THEN
        RAISE EXCEPTION 'lý do đang bật không đúng 4 mã mong đợi: %', v_active;
    END IF;

    FOR v_bad IN
        SELECT category, COUNT(*) AS n
          FROM rejection_reason WHERE is_active
         GROUP BY category HAVING COUNT(*) <> 1
    LOOP
        RAISE EXCEPTION 'nhóm % có % mã đang bật (phải đúng 1)', v_bad.category, v_bad.n;
    END LOOP;

    RAISE NOTICE 'OK  4 lý do đang bật, mỗi nhóm đúng một mã';
END $$;

-- ------------------------------------------------------------------
-- 2. Năm mã cũ vẫn còn dòng (chỉ ẩn, không xoá).
-- ------------------------------------------------------------------
DO $$
DECLARE
    v_missing TEXT[];
BEGIN
    SELECT array_agg(code) INTO v_missing
      FROM unnest(ARRAY['script_text_error','word_skipped_added','low_volume',
                        'clipping_distortion','excess_silence']::TEXT[]) AS code
     WHERE NOT EXISTS (
         SELECT 1 FROM rejection_reason r
          WHERE r.reason_code = code AND NOT r.is_active);

    IF v_missing IS NOT NULL THEN
        RAISE EXCEPTION 'mã cũ bị mất hoặc chưa ẩn: %', v_missing;
    END IF;

    RAISE NOTICE 'OK  5 mã cũ vẫn còn dòng nhưng đã ẩn';
END $$;

-- ------------------------------------------------------------------
-- 3. Lịch sử không còn trỏ vào mã đã ẩn, thống kê chỉ còn 4 mã.
-- ------------------------------------------------------------------
DO $$
DECLARE
    v_orphans INT;
    v_stats   TEXT[];
BEGIN
    SELECT COUNT(*) INTO v_orphans
      FROM review_rejection_reason rrr
      JOIN rejection_reason rr ON rr.reason_id = rrr.reason_id
     WHERE NOT rr.is_active;

    IF v_orphans > 0 THEN
        RAISE EXCEPTION 'còn % dòng lịch sử trỏ vào lý do đã ẩn', v_orphans;
    END IF;

    SELECT array_agg(DISTINCT reason_code) INTO v_stats FROM v_rejection_reason_stats;

    IF v_stats IS NOT NULL AND NOT (v_stats <@ ARRAY['background_noise','content_mismatch',
                                                     'mispronunciation','other']::TEXT[]) THEN
        RAISE EXCEPTION 'thống kê còn mã ngoài 4 lý do: %', v_stats;
    END IF;

    RAISE NOTICE 'OK  lịch sử và thống kê chỉ còn 4 lý do';
END $$;

-- ------------------------------------------------------------------
-- 4. Hành vi: mã đã ẩn không được dùng khi từ chối — chặn ở tầng service
--    (rejection_reason_inactive), còn ở đây ghim điều kiện tra cứu mà
--    service dựa vào: GetReasonsByCodesAsync trả về is_active đúng.
-- ------------------------------------------------------------------
DO $$
DECLARE
    v_hidden BOOLEAN;
    v_kept   BOOLEAN;
BEGIN
    SELECT NOT is_active INTO v_hidden FROM rejection_reason WHERE reason_code = 'low_volume';
    SELECT is_active     INTO v_kept   FROM rejection_reason WHERE reason_code = 'content_mismatch';

    IF v_hidden IS DISTINCT FROM TRUE OR v_kept IS DISTINCT FROM TRUE THEN
        RAISE EXCEPTION 'cờ is_active sai (low_volume ẩn = %, content_mismatch bật = %)', v_hidden, v_kept;
    END IF;

    RAISE NOTICE 'OK  cờ is_active đúng cho mã bị ẩn và mã giữ lại';
END $$;

ROLLBACK;
