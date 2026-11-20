-- V34: task đóng góp câu, khóa hạn cứng, liên kết câu–task, nơi sinh và đồng ý.

ALTER TYPE task_type ADD VALUE IF NOT EXISTS 'contribution';

ALTER TABLE task
    ADD COLUMN IF NOT EXISTS enforce_deadline BOOLEAN NOT NULL DEFAULT FALSE;

ALTER TABLE script
    ADD COLUMN IF NOT EXISTS contribution_task_id BIGINT;

ALTER TABLE script
    DROP CONSTRAINT IF EXISTS fk_script_contribution_task;
ALTER TABLE script
    ADD CONSTRAINT fk_script_contribution_task
    FOREIGN KEY (contribution_task_id) REFERENCES task(task_id) ON DELETE SET NULL;

CREATE INDEX IF NOT EXISTS idx_script_contribution_task ON script (contribution_task_id);

ALTER TABLE speaker_profile
    ADD COLUMN IF NOT EXISTS birth_place VARCHAR(100);

CREATE TABLE IF NOT EXISTS user_consent (
    user_id       BIGINT PRIMARY KEY REFERENCES app_user(user_id) ON DELETE CASCADE,
    version       VARCHAR(32) NOT NULL,
    accepted_at   TIMESTAMPTZ NOT NULL DEFAULT now(),
    document_key  TEXT,
    document_name VARCHAR(255),
    CONSTRAINT ck_user_consent_document
        CHECK (document_key IS NULL OR document_name IS NOT NULL)
);
