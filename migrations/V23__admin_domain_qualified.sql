-- ============================================================
-- V23 — Admin đủ tư cách duyệt mọi chủ đề
--
-- Luật cũ: câu chỉ được chuyển 'validated' khi có lượt duyệt
-- accepted/edited của người có dòng user_domain đúng chủ đề.
-- Admin không được phân chủ đề ở màn quản lý người dùng, nên
-- Admin duyệt câu xong trigger ném lỗi thô từ database — trong
-- khi service lại cho qua (IsQualifiedAsync cũng chỉ đọc
-- user_domain).
--
-- Luật mới: role admin đủ mọi chủ đề; các vai khác vẫn phải có
-- dòng user_domain khớp. Chỉ thay thân hàm
-- fn_script_validated_by_domain_reviewer. Trigger
-- trg_script_validated_domain giữ nguyên, không đụng bảng hay cột.
--
-- Idempotent: chạy lại nhiều lần vẫn cho kết quả như nhau.
-- ============================================================

CREATE OR REPLACE FUNCTION fn_script_validated_by_domain_reviewer() RETURNS trigger AS $$ BEGIN
    IF NEW.status = 'validated'
       AND (TG_OP = 'INSERT' OR OLD.status IS DISTINCT FROM 'validated') THEN
        IF NOT EXISTS (
            SELECT 1
              FROM script_review sr
              JOIN app_user u ON u.user_id = sr.user_id
              JOIN role r ON r.role_id = u.role_id
             WHERE sr.script_id = NEW.script_id
               AND sr.action IN ('accepted','edited')
               AND (TG_OP = 'INSERT' OR sr.reviewed_at >= OLD.updated_at)
               AND (r.role_name = 'admin'
                    OR EXISTS (
                        SELECT 1 FROM user_domain ud
                         WHERE ud.user_id = sr.user_id
                           AND ud.domain = NEW.domain))
        ) THEN
            RAISE EXCEPTION
                'script % cannot be validated: no accepted/edited review by a reviewer qualified for domain % after last reset',
                NEW.script_id, NEW.domain;
        END IF;
    END IF;
    RETURN NEW;
END $$ LANGUAGE plpgsql;
