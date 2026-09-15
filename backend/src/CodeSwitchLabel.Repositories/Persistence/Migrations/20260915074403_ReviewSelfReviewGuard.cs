using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodeSwitchLabel.Repositories.Persistence.Migrations
{
    /// <summary>
    /// Trigger chặn Reviewer duyệt bản ghi do chính mình thu.
    ///
    /// Tầng Service đã kiểm quy tắc này và trả lỗi 403 tử tế. Trigger là lưới chắn cuối:
    /// nó vẫn chặn được khi có ai ghi thẳng vào database, hoặc khi một tài khoản bị đổi vai —
    /// ví dụ một Speaker được chuyển sang làm Reviewer rồi nhận trúng bản ghi cũ của chính mình.
    ///
    /// Viết bằng SQL thô vì EF Core không có API sinh trigger.
    /// CHECK constraint không làm được việc này: CHECK chỉ nhìn thấy các cột của chính hàng
    /// đang ghi, còn quy tắc này bắt buộc phải nhìn sang bảng recording.
    /// </summary>
    public partial class ReviewSelfReviewGuard : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION fn_review_not_self() RETURNS trigger AS $$
                BEGIN
                  IF EXISTS (
                    SELECT 1
                    FROM   recording r
                    WHERE  r.recording_id = NEW.recording_id
                      AND  r.speaker_id   = NEW.reviewer_id
                  ) THEN
                    RAISE EXCEPTION 'Reviewer % là người thu bản ghi % — không được tự duyệt',
                                    NEW.reviewer_id, NEW.recording_id
                      USING ERRCODE = 'check_violation';
                  END IF;

                  RETURN NEW;
                END
                $$ LANGUAGE plpgsql;
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER trg_review_not_self
                BEFORE INSERT OR UPDATE OF reviewer_id, recording_id ON review
                FOR EACH ROW EXECUTE FUNCTION fn_review_not_self();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_review_not_self ON review;");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS fn_review_not_self();");
        }
    }
}
