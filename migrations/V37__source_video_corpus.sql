-- V37: corpus hội thoại từ YouTube.
-- Bảng riêng, không dùng recording: dữ liệu là đoạn hội thoại dài với câu đã tách,
-- không ép về cặp [cs, vi].

BEGIN;

CREATE TYPE source_video_status AS ENUM
    ('queued','processing','ready_for_review','in_review','approved','failed');

CREATE TABLE source_video (
    source_video_id  BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    youtube_url      VARCHAR(1024) NOT NULL UNIQUE,
    youtube_video_id VARCHAR(32),
    title            VARCHAR(500),
    duration_sec     NUMERIC(10,2) CHECK (duration_sec IS NULL OR duration_sec > 0),
    audio_key        VARCHAR(1024),
    transcript       TEXT,
    status           source_video_status NOT NULL DEFAULT 'queued',
    error_message    TEXT,
    attempts         INT NOT NULL DEFAULT 0 CHECK (attempts >= 0),
    submitted_by     BIGINT NOT NULL REFERENCES app_user(user_id),
    claimed_by       BIGINT REFERENCES app_user(user_id),
    claim_expires_at TIMESTAMPTZ,
    approved_at      TIMESTAMPTZ,
    created_at       TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at       TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT ck_source_video_approved CHECK (
        status <> 'approved'
        OR (approved_at IS NOT NULL AND audio_key IS NOT NULL)),
    CONSTRAINT ck_source_video_claim CHECK (
        status <> 'in_review'
        OR (claimed_by IS NOT NULL AND claim_expires_at IS NOT NULL))
);

CREATE UNIQUE INDEX uq_source_video_youtube ON source_video (youtube_video_id)
    WHERE youtube_video_id IS NOT NULL;
CREATE INDEX idx_source_video_status ON source_video (status, created_at);

CREATE TABLE source_segment (
    source_segment_id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    source_video_id   BIGINT NOT NULL REFERENCES source_video(source_video_id) ON DELETE CASCADE,
    position          INT NOT NULL CHECK (position > 0),
    start_sec         NUMERIC(10,3) NOT NULL CHECK (start_sec >= 0),
    end_sec           NUMERIC(10,3) NOT NULL CHECK (end_sec > start_sec),
    text              TEXT NOT NULL CHECK (btrim(text) <> ''),
    language          VARCHAR(8) CHECK (language IN ('vi','en')),
    clip_key          VARCHAR(1024),
    UNIQUE (source_video_id, position)
);

CREATE TRIGGER trg_source_video_touch
    BEFORE UPDATE ON source_video
    FOR EACH ROW EXECUTE FUNCTION fn_touch_updated_at();

CREATE TRIGGER trg_audit_source_video
    AFTER INSERT OR UPDATE OR DELETE ON source_video
    FOR EACH ROW EXECUTE FUNCTION fn_audit('source_video', 'source_video_id');

COMMIT;
