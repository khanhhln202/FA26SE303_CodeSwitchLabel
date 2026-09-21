using CodeSwitchLabel.Repositories.Enums;

namespace CodeSwitchLabel.Repositories.Entities;

public class WorkTask
{
    public long TaskId { get; set; }
    public long CreatedBy { get; set; }
    public TaskType TaskType { get; set; }
    public string? Description { get; set; }

    /// <summary>Task thu âm đếm theo CẶP CÂU; task duyệt đếm theo bản ghi.</summary>
    public int TargetQty { get; set; }

    public DateTimeOffset? Deadline { get; set; }
    public WorkTaskStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public AppUser Creator { get; set; } = null!;
    public ICollection<TaskAssignment> Assignments { get; set; } = [];
    public ICollection<TaskScript> TaskScripts { get; set; } = [];
    public ICollection<TaskRecording> TaskRecordings { get; set; } = [];
}

/// <summary>
/// Mỗi lượt giao là một dòng riêng nên giữ được lịch sử ai từng nhận task.
/// Index duy nhất có điều kiện của lược đồ chỉ cho phép một lượt active trên mỗi task.
/// </summary>
public class TaskAssignment
{
    public long AssignmentId { get; set; }
    public long TaskId { get; set; }
    public long UserId { get; set; }
    public DateTimeOffset AssignedAt { get; set; }
    public AssignmentStatus AssignmentStatus { get; set; }

    public WorkTask Task { get; set; } = null!;
    public AppUser User { get; set; } = null!;
}

public class TaskScript
{
    public long TaskId { get; set; }
    public string ScriptId { get; set; } = string.Empty;
    public TaskScriptStatus Status { get; set; }
    public DateTimeOffset IncludedAt { get; set; }

    public WorkTask Task { get; set; } = null!;
    public Script Script { get; set; } = null!;
}

public class TaskRecording
{
    public long TaskId { get; set; }
    public string RecordingId { get; set; } = string.Empty;
    public TaskRecordingStatus Status { get; set; }
    public DateTimeOffset IncludedAt { get; set; }

    public WorkTask Task { get; set; } = null!;
    public Recording Recording { get; set; } = null!;
}
