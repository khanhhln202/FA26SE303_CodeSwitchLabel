using CodeSwitchLabel.Repositories.Enums;

namespace CodeSwitchLabel.Repositories.Entities;

/// <summary>
/// Chiến dịch thu thập — đơn vị kế hoạch của Task Manager cho một đợt (thường 1-2 tuần).
/// Task có thể độc lập (campaign_id NULL) rồi gắn vào chiến dịch sau, hoặc gỡ ra mà không bị xoá.
/// Chỉ tiêu và thời gian chỉ ràng buộc khi task đang gắn với chiến dịch: tổng chỉ tiêu task
/// không được vượt chỉ tiêu chiến dịch, và hạn task phải nằm trong khoảng ngày của chiến dịch.
/// </summary>
public class Campaign
{
    public long CampaignId { get; set; }
    public string CampaignName { get; set; } = string.Empty;

    /// <summary>
    /// Chủ đề của cả đợt. Một đợt chỉ đi trên một chủ đề — trigger
    /// trg_task_campaign_domain chặn gắn nhiệm vụ khác chủ đề vào đợt này.
    /// </summary>
    public ScriptDomain Domain { get; set; }

    /// <summary>Chỉ tiêu cặp câu cho cả chiến dịch; lược đồ giới hạn 2000..5000.</summary>
    public int TargetQty { get; set; }

    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public CampaignStatus Status { get; set; }

    /// <summary>
    /// Người tạo phải mang vai admin hoặc task_manager, do trigger của database chặn.
    /// Task Manager tự lập kế hoạch cho đợt của mình nên cũng tạo được đợt.
    /// </summary>
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

    /// <summary>NULL = task độc lập, chưa gắn chiến dịch nào; gắn sau hoặc gỡ ra đều được.</summary>
    public long? CampaignId { get; set; }

    public long CreatedBy { get; set; }
    public TaskType TaskType { get; set; }

    /// <summary>
    /// Chủ đề của nhiệm vụ. Task độc lập thì tự do; task gắn vào đợt thì phải
    /// trùng <see cref="Campaign.Domain"/> — database chặn, không chỉ service.
    /// </summary>
    public ScriptDomain Domain { get; set; }

    public string? Description { get; set; }

    /// <summary>Task thu âm đếm theo CẶP CÂU; task duyệt đếm theo bản ghi.</summary>
    public int TargetQty { get; set; }

    public DateTimeOffset? Deadline { get; set; }
    public WorkTaskStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public Campaign? Campaign { get; set; }
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

    /// <summary>
    /// Chỉ tiêu riêng của người nhận trong task. NULL = lượt giao cũ chưa có quota;
    /// khi đọc tiến độ thì chia đều <see cref="WorkTask.TargetQty"/> cho số người nhận.
    /// </summary>
    public int? TargetQty { get; set; }

    public WorkTask Task { get; set; } = null!;
    public AppUser User { get; set; } = null!;
}

public class TaskScript
{
    /// <summary>Khoá thay thế: cùng một cặp câu có thể xuất hiện một lần cho mỗi Speaker.</summary>
    public long TaskScriptId { get; set; }

    public long TaskId { get; set; }
    public string ScriptId { get; set; } = string.Empty;

    /// <summary>
    /// Speaker được gán cặp câu này để hiển thị/chia quota. Nhiều Speaker có thể nhận
    /// cùng một cặp câu; mỗi người chỉ một dòng. NULL = chưa gán / task duyệt.
    /// </summary>
    public long? AssigneeUserId { get; set; }

    public TaskScriptStatus Status { get; set; }
    public DateTimeOffset IncludedAt { get; set; }

    public WorkTask Task { get; set; } = null!;
    public Script Script { get; set; } = null!;
    public AppUser? AssigneeUser { get; set; }
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
