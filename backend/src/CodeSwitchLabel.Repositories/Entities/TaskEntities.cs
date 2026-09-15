using CodeSwitchLabel.Repositories.Enums;

namespace CodeSwitchLabel.Repositories.Entities;

/// <summary>
/// Task do Task Manager tạo. Tên là WorkTask để khỏi đụng System.Threading.Tasks.Task;
/// bảng dưới database vẫn là "task" đúng như ERD.
/// </summary>
public class WorkTask
{
    public long TaskId { get; set; }

    public long CreatedById { get; set; }
    public AppUser CreatedBy { get; set; } = null!;

    public TaskType TaskType { get; set; }
    public string? Description { get; set; }

    public int TargetQty { get; set; }
    public DateTimeOffset Deadline { get; set; }
    public WorkTaskStatus Status { get; set; } = WorkTaskStatus.Draft;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<TaskAssignment> Assignments { get; set; } = [];
    public ICollection<TaskScript> TaskScripts { get; set; } = [];
    public ICollection<TaskRecording> TaskRecordings { get; set; } = [];
}

/// <summary>
/// Giao task cho một người. Khoá chính là cột thay thế chứ không phải (task, user),
/// nên điều phối lại nhiều lần vẫn giữ được toàn bộ lịch sử ai từng nhận task này.
/// </summary>
public class TaskAssignment
{
    public long AssignmentId { get; set; }

    public long TaskId { get; set; }
    public WorkTask Task { get; set; } = null!;

    public long UserId { get; set; }
    public AppUser User { get; set; } = null!;

    public DateTimeOffset AssignedAt { get; set; } = DateTimeOffset.UtcNow;
    public AssignmentStatus AssignmentStatus { get; set; } = AssignmentStatus.Active;
}

/// <summary>Danh sách script thuộc một task thu âm. Task Manager chốt sẵn phạm vi.</summary>
public class TaskScript
{
    public long TaskId { get; set; }
    public WorkTask Task { get; set; } = null!;

    public long ScriptId { get; set; }
    public Script Script { get; set; } = null!;

    public TaskScriptStatus Status { get; set; } = TaskScriptStatus.Pending;
    public DateTimeOffset IncludedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>Danh sách bản ghi thuộc một task duyệt.</summary>
public class TaskRecording
{
    public long TaskId { get; set; }
    public WorkTask Task { get; set; } = null!;

    public long RecordingId { get; set; }
    public Recording Recording { get; set; } = null!;

    public TaskRecordingStatus Status { get; set; } = TaskRecordingStatus.Queued;
    public DateTimeOffset IncludedAt { get; set; } = DateTimeOffset.UtcNow;
}
