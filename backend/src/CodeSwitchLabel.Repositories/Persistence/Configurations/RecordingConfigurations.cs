using CodeSwitchLabel.Repositories.Entities;
using CodeSwitchLabel.Repositories.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CodeSwitchLabel.Repositories.Persistence.Configurations;

public class RecordingConfiguration : IEntityTypeConfiguration<Recording>
{
    public void Configure(EntityTypeBuilder<Recording> b)
    {
        b.ToTable("recording", t =>
        {
            t.HasCheckConstraint("ck_recording_duration", "duration_sec > 0");
        });

        b.HasKey(x => x.RecordingId);

        b.Property(x => x.S3Key).IsRequired().HasMaxLength(512);
        b.Property(x => x.AudioFormat).IsRequired().HasMaxLength(10).HasDefaultValue("wav");
        b.Property(x => x.DurationSec).HasPrecision(8, 2);

        b.HasOne(x => x.Script)
            .WithMany(s => s.Recordings)
            .HasForeignKey(x => x.ScriptId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.Speaker)
            .WithMany()
            .HasForeignKey(x => x.SpeakerId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.Task)
            .WithMany()
            .HasForeignKey(x => x.TaskId)
            .OnDelete(DeleteBehavior.SetNull);

        // Một file trong object storage chỉ thuộc về đúng một bản ghi.
        b.HasIndex(x => x.S3Key).IsUnique().HasDatabaseName("ux_recording_s3_key");

        b.HasIndex(x => x.ScriptId).HasDatabaseName("ix_recording_script");
        b.HasIndex(x => x.SpeakerId).HasDatabaseName("ix_recording_speaker");
        b.HasIndex(x => x.TaskId).HasDatabaseName("ix_recording_task");

        // Hàng đợi của Reviewer — phần nhỏ nhưng được hỏi liên tục.
        b.HasIndex(x => x.RecordedAt)
            .HasFilter($"status = {(int)RecordingStatus.PendingReview}")
            .HasDatabaseName("ix_recording_pending");
    }
}

public class ReviewConfiguration : IEntityTypeConfiguration<Review>
{
    public void Configure(EntityTypeBuilder<Review> b)
    {
        b.ToTable("review", t =>
        {
            t.HasCheckConstraint("ck_review_round", "review_round BETWEEN 1 AND 3");
        });

        b.HasKey(x => x.ReviewId);
        b.Property(x => x.Comment).HasMaxLength(1000);

        b.HasOne(x => x.Recording)
            .WithMany(r => r.Reviews)
            .HasForeignKey(x => x.RecordingId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.Reviewer)
            .WithMany()
            .HasForeignKey(x => x.ReviewerId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.Task)
            .WithMany()
            .HasForeignKey(x => x.TaskId)
            .OnDelete(DeleteBehavior.SetNull);

        // Mỗi người duyệt một bản ghi đúng một lần trong mỗi vòng.
        // Muốn duyệt lại thì phải là vòng khác.
        b.HasIndex(x => new { x.RecordingId, x.ReviewerId, x.ReviewRound })
            .IsUnique()
            .HasDatabaseName("ux_review_recording_reviewer_round");

        b.HasIndex(x => x.ReviewerId).HasDatabaseName("ix_review_reviewer");
        b.HasIndex(x => x.TaskId).HasDatabaseName("ix_review_task");

        b.HasIndex(x => x.RecordingId)
            .HasFilter($"decision = {(int)ReviewDecision.Rejected}")
            .HasDatabaseName("ix_review_rejected");
    }
}

public class RejectionReasonConfiguration : IEntityTypeConfiguration<RejectionReason>
{
    public void Configure(EntityTypeBuilder<RejectionReason> b)
    {
        b.ToTable("rejection_reason");
        b.HasKey(x => x.ReasonId);

        b.Property(x => x.ReasonCode).IsRequired().HasMaxLength(50);
        b.Property(x => x.Description).HasMaxLength(500);

        b.HasIndex(x => x.ReasonCode).IsUnique().HasDatabaseName("ux_rejection_reason_code");
    }
}

public class ReviewRejectionReasonConfiguration : IEntityTypeConfiguration<ReviewRejectionReason>
{
    public void Configure(EntityTypeBuilder<ReviewRejectionReason> b)
    {
        b.ToTable("review_rejection_reason");
        b.HasKey(x => new { x.ReviewId, x.ReasonId });

        b.HasOne(x => x.Review)
            .WithMany(r => r.RejectionReasons)
            .HasForeignKey(x => x.ReviewId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.Reason)
            .WithMany(r => r.Reviews)
            .HasForeignKey(x => x.ReasonId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => x.ReasonId).HasDatabaseName("ix_review_rejection_reason_reason");
    }
}
