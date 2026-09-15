using CodeSwitchLabel.Repositories.Entities;
using CodeSwitchLabel.Repositories.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CodeSwitchLabel.Repositories.Persistence.Configurations;

public class ScriptConfiguration : IEntityTypeConfiguration<Script>
{
    public void Configure(EntityTypeBuilder<Script> b)
    {
        b.ToTable("script", t =>
        {
            t.HasCheckConstraint("ck_script_word_count", "word_count >= 0");
            t.HasCheckConstraint("ck_script_en_word_count", "en_word_count >= 0");

            // Số từ tiếng Anh không thể nhiều hơn tổng số từ. Ràng buộc này
            // không có trong ERD nhưng suy ra được từ chính định nghĩa hai cột.
            t.HasCheckConstraint("ck_script_en_not_exceed_total", "en_word_count <= word_count");
        });

        b.HasKey(x => x.ScriptId);
        b.Property(x => x.Content).IsRequired();

        b.HasOne(x => x.CreatedBy)
            .WithMany()
            .HasForeignKey(x => x.CreatedById)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.ImportBatch)
            .WithMany(i => i.Scripts)
            .HasForeignKey(x => x.ImportBatchId)
            .OnDelete(DeleteBehavior.SetNull);

        b.HasIndex(x => x.CreatedById).HasDatabaseName("ix_script_created_by");

        // Truy vấn nóng nhất: lấy script đã duyệt để phát cho Speaker.
        b.HasIndex(x => new { x.Status, x.Domain }).HasDatabaseName("ix_script_status_domain");

        // Chỉ đánh index phần đang chờ duyệt — phần này nhỏ và được hỏi liên tục,
        // trong khi toàn bảng thì lớn dần theo thời gian.
        b.HasIndex(x => x.CreatedAt)
            .HasFilter($"status = {(int)ScriptStatus.PendingValidation}")
            .HasDatabaseName("ix_script_pending");
    }
}

public class ImportBatchConfiguration : IEntityTypeConfiguration<ImportBatch>
{
    public void Configure(EntityTypeBuilder<ImportBatch> b)
    {
        b.ToTable("import_batch", t =>
        {
            t.HasCheckConstraint("ck_import_batch_row_count", "row_count >= 0");
        });

        b.HasKey(x => x.BatchId);
        b.Property(x => x.FileName).IsRequired().HasMaxLength(255);

        b.HasOne(x => x.ImportedBy)
            .WithMany()
            .HasForeignKey(x => x.ImportedById)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class ScriptReviewConfiguration : IEntityTypeConfiguration<ScriptReview>
{
    public void Configure(EntityTypeBuilder<ScriptReview> b)
    {
        b.ToTable("script_review", t =>
        {
            // Sửa nội dung thì phải có nội dung mới; không sửa thì không được điền.
            // Hai chiều chứ không chỉ một, để không ai gửi edited_content kèm action = accepted.
            t.HasCheckConstraint("ck_script_review_edited",
                $"(action = {(int)ScriptReviewAction.Edited}) = (edited_content IS NOT NULL)");

            // Từ chối thì bắt buộc nêu lý do từ danh mục.
            t.HasCheckConstraint("ck_script_review_reason",
                $"action <> {(int)ScriptReviewAction.Rejected} OR error_reason_id IS NOT NULL");
        });

        b.HasKey(x => x.ScriptReviewId);
        b.Property(x => x.Comment).HasMaxLength(1000);

        b.HasOne(x => x.Script)
            .WithMany(s => s.Reviews)
            .HasForeignKey(x => x.ScriptId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.ErrorReason)
            .WithMany(r => r.ScriptReviews)
            .HasForeignKey(x => x.ErrorReasonId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => x.ScriptId).HasDatabaseName("ix_script_review_script");
        b.HasIndex(x => x.UserId).HasDatabaseName("ix_script_review_user");
    }
}

public class ScriptErrorReasonConfiguration : IEntityTypeConfiguration<ScriptErrorReason>
{
    public void Configure(EntityTypeBuilder<ScriptErrorReason> b)
    {
        b.ToTable("script_error_reason");
        b.HasKey(x => x.ReasonId);

        b.Property(x => x.ReasonCode).IsRequired().HasMaxLength(50);
        b.Property(x => x.Description).HasMaxLength(500);

        b.HasIndex(x => x.ReasonCode).IsUnique().HasDatabaseName("ux_script_error_reason_code");
    }
}

public class ScriptSkipConfiguration : IEntityTypeConfiguration<ScriptSkip>
{
    public void Configure(EntityTypeBuilder<ScriptSkip> b)
    {
        b.ToTable("script_skip");
        b.HasKey(x => x.SkipId);

        b.HasOne(x => x.Script)
            .WithMany(s => s.Skips)
            .HasForeignKey(x => x.ScriptId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.Speaker)
            .WithMany()
            .HasForeignKey(x => x.SpeakerId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.Task)
            .WithMany()
            .HasForeignKey(x => x.TaskId)
            .OnDelete(DeleteBehavior.SetNull);

        // Một người bỏ qua một script đúng một lần. Bỏ qua hai lần là vô nghĩa,
        // và nếu để trùng thì truy vấn "đừng phát lại" phải lọc trùng.
        b.HasIndex(x => new { x.ScriptId, x.SpeakerId })
            .IsUnique()
            .HasDatabaseName("ux_script_skip_speaker");
    }
}
