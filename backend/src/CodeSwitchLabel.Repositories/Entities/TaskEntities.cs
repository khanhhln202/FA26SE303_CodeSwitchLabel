using CodeSwitchLabel.Repositories.Enums;

namespace CodeSwitchLabel.Repositories.Entities;

/// <summary>
/// Chiến dịch thu thập — đơn vị kế hoạch của Task Manager cho một đợt (thường 1-2 tuần).
/// Mọi task đều thuộc một chiến dịch. Chỉ tiêu và thời gian do trigger của database ràng buộc
/// với các task con: tổng chỉ tiêu task không được vượt chỉ tiêu chiến dịch, và hạn task phải
/// nằm trong khoảng ngày của chiến dịch.
/// </summary>
public class Campaign
{
    public long CampaignId { get; set; }
    public string CampaignName { get; set; } = string.Empty;

    /// <summary>Chỉ tiêu cặp câu cho cả chiến dịch; lược đồ giới hạn 2000..5000.</summary>
    public int TargetQty { get; set; }

    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public CampaignStatus Status { get; set; }

    /// <summary>Người tạo phải mang vai admin, do trigger của database chặn.</summary>
    public long CreatedBy { get; set; }

    /// <summary>Task Manager được giao phụ trách chiến dịch (NULL = chưa giao). Chỉ Admin mới được set.</summary>
    public long? AssignedTo { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public AppUser Creator { get; set; } = null!;
    public AppUser? AssignedToUser { get; set; }
    public ICollection<WorkTask> Tasks { get; set; } = [];
}

public class WorkTask
{
    public long TaskId { get; set; }

    /// <summary>Mỗi task phải thuộc một chiến dịch — lược đồ đặt campaign_id là NOT NULL.</summary>
    public long CampaignId { get; set; }

    public long CreatedBy { get; set; }
    public TaskType TaskType { get; set; }
    public string? Description { get; set; }

    /// <summary>Task thu âm đếm theo CẶP CÂU; task duyệt đếm theo bản ghi.</summary>
    public int TargetQty { get; set; }

    public DateTimeOffset? Deadline { get; set; }
    public WorkTaskStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public Campaign Campaign { get; set; } = null!;
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

/// <summary>Speaker đăng ký tham gia một chiến dịch ("Đợt" ở FE) — không giới hạn số người.</summary>
public class CampaignRegistration
{
    public long CampaignId { get; set; }
    public long SpeakerId { get; set; }
    public DateTimeOffset RegisteredAt { get; set; }

    public Campaign Campaign { get; set; } = null!;
    public AppUser Speaker { get; set; } = null!;
}
