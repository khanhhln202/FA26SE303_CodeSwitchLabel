-- V35: đóng góp/báo lỗi thống nhất, đệm đợt, trả bài thu âm về kho của đợt khác người đọc.

ALTER TABLE wave
    ADD COLUMN IF NOT EXISTS buffer_qty INT NOT NULL DEFAULT 0;

ALTER TABLE wave
    ADD COLUMN IF NOT EXISTS imported_qty INT NOT NULL DEFAULT 0;

ALTER TABLE task
    ADD COLUMN IF NOT EXISTS imported_qty INT NOT NULL DEFAULT 0;

ALTER TABLE script
    ADD COLUMN IF NOT EXISTS wave_id BIGINT REFERENCES wave(wave_id) ON DELETE SET NULL;

ALTER TABLE script
    ADD COLUMN IF NOT EXISTS contributor_comment TEXT;

CREATE INDEX IF NOT EXISTS idx_script_wave ON script (wave_id);

-- Trả bài thu âm bị từ chối về kho của đợt: tạo mục task_script cho người nhận khác
-- trong cùng đợt chưa thu cặp câu này. ON CONFLICT DO NOTHING an toàn dưới cạnh tranh.
CREATE OR REPLACE FUNCTION fn_backfill_recording_task_from_wave(p_task_id BIGINT, p_count INT)
RETURNS INT LANGUAGE plpgsql AS $$
DECLARE v_added INT := 0; v_wave BIGINT; v_domain TEXT; v_capacity INT; v_assigned INT;
BEGIN
  SELECT t.wave_id, t.domain, w.target_qty + w.buffer_qty
    INTO v_wave, v_domain, v_capacity
    FROM task t JOIN wave w ON w.wave_id = t.wave_id
   WHERE t.task_id = p_task_id AND t.task_type = 'recording';
  IF v_wave IS NULL THEN RETURN 0; END IF;

  SELECT COUNT(DISTINCT ts.script_id) INTO v_assigned
    FROM task_script ts JOIN task t ON t.task_id = ts.task_id
   WHERE t.wave_id = v_wave AND t.task_type = 'recording'
     AND ts.status <> 'rejected';
  p_count := LEAST(p_count, GREATEST(0, v_capacity - v_assigned));
  IF p_count <= 0 THEN RETURN 0; END IF;

  INSERT INTO task_script (task_id, script_id, status, included_at, assignee_user_id)
  SELECT p_task_id, s.script_id, 'pending', now(), NULL
  FROM script s
  WHERE s.status = 'validated' AND s.domain = v_domain AND s.wave_id = v_wave
    AND s.wave_id IS NOT NULL
    AND NOT EXISTS (
      SELECT 1 FROM task_script ts
      JOIN task t2 ON t2.task_id = ts.task_id
      WHERE ts.script_id = s.script_id
        AND t2.wave_id = v_wave
        AND t2.task_type = 'recording'
    )
  ORDER BY s.created_at, s.script_id
  LIMIT p_count
  ON CONFLICT (task_id, script_id) WHERE assignee_user_id IS NULL DO NOTHING;

  GET DIAGNOSTICS v_added = ROW_COUNT;
  RETURN v_added;
END $$;
