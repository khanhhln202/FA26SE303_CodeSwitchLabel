using CodeSwitchLabel.Repositories.Entities;
using CodeSwitchLabel.Repositories.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CodeSwitchLabel.Repositories.Persistence.Configurations;

public class WorkTaskConfiguration : IEntityTypeConfiguration<WorkTask>
{
    public void Configure(EntityTypeBuilder<WorkTask> b)
    {
        b.ToTable("task", t =>
        {
            t.HasCheckConstraint("ck_task_target_qty", "target_qty > 0");
        });

        b.HasKey(x => x.TaskId);
        b.Property(x => x.Description).HasMaxLength(1000);

        b.HasOne(x => x.CreatedBy)
            .WithMany()
            .HasForeignKey(x => x.CreatedById)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => x.CreatedById).HasDatabaseName("ix_task_created_by");

        // Chỉ index các task còn đang chạy — đó là phần duy nhất màn hình theo dõi hỏi tới.
        b.HasIndex(x => x.Deadline)
            .HasFilter($"status IN ({(int)WorkTaskStatus.Open}, {(int)WorkTaskStatus.InProgress})")
            .HasDatabaseName("ix_task_open_deadline");
    }
}

public class TaskAssignmentConfiguration : IEntityTypeConfiguration<TaskAssignment>
{
    public void Configure(EntityTypeBuilder<TaskAssignment> b)
    {
        b.ToTable("task_assignment");
        b.HasKey(x => x.AssignmentId);

        b.HasOne(x => x.Task)
            .WithMany(t => t.Assignments)
            .HasForeignKey(x => x.TaskId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => x.UserId).HasDatabaseName("ix_task_assignment_user");

        // Một task chỉ có đúng MỘT người đang nhận tại một thời điểm.
        // Các lượt giao cũ vẫn nằm lại với trạng thái reassigned, nên lịch sử không mất.
        b.HasIndex(x => x.TaskId)
            .IsUnique()
            .HasFilter($"assignment_status = {(int)AssignmentStatus.Active}")
            .HasDatabaseName("ux_task_active_assignment");
    }
}

public class TaskScriptConfiguration : IEntityTypeConfiguration<TaskScript>
{
    public void Configure(EntityTypeBuilder<TaskScript> b)
    {
        b.ToTable("task_script");

        // Khoá chính ghép, đúng như ERD — không cần cột id thay thế
        // vì một cặp (task, script) chỉ xuất hiện một lần.
        b.HasKey(x => new { x.TaskId, x.ScriptId });

        b.HasOne(x => x.Task)
            .WithMany(t => t.TaskScripts)
            .HasForeignKey(x => x.TaskId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.Script)
            .WithMany(s => s.TaskScripts)
            .HasForeignKey(x => x.ScriptId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => x.ScriptId).HasDatabaseName("ix_task_script_script");
    }
}

public class TaskRecordingConfiguration : IEntityTypeConfiguration<TaskRecording>
{
    public void Configure(EntityTypeBuilder<TaskRecording> b)
    {
        b.ToTable("task_recording");
        b.HasKey(x => new { x.TaskId, x.RecordingId });

        b.HasOne(x => x.Task)
            .WithMany(t => t.TaskRecordings)
            .HasForeignKey(x => x.TaskId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.Recording)
            .WithMany(r => r.TaskRecordings)
            .HasForeignKey(x => x.RecordingId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => x.RecordingId).HasDatabaseName("ix_task_recording_recording");
    }
}
