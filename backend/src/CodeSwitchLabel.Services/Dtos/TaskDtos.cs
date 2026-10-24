using System.ComponentModel.DataAnnotations;
using CodeSwitchLabel.Repositories.Enums;
using CodeSwitchLabel.Services.Abstractions;

namespace CodeSwitchLabel.Services.Dtos;

public record CreateTaskRequest
{
    /// <summary>Để trống (null) = tạo task độc lập, gắn vào chiến dịch sau. Có giá trị thì kiểm tra quota/hạn như cũ.</summary>
    [Range(1, long.MaxValue, ErrorMessage = "Chiến dịch không hợp lệ.")]
    public long? CampaignId { get; init; }

    [Required(ErrorMessage = "Phải chọn loại task: Recording hoặc Review.")]
    public TaskType? TaskType { get; init; }

    /// <summary>
    /// Chủ đề của nhiệm vụ. Khi gắn vào chiến dịch, phải trùng chủ đề của chiến dịch —
    /// nếu không thì 422 task_domain_mismatch_campaign.
    /// </summary>
    public ScriptDomain? Domain { get; init; }

    [StringLength(1000)]
    public string? Description { get; init; }

    /// <summary>Task thu âm: số CẶP CÂU. Task duyệt: số bản ghi.</summary>
    [Required(ErrorMessage = "Phải đặt chỉ tiêu.")]
    [Range(1, 100_000)]
    public int? TargetQty { get; init; }

    /// <summary>Gửi kèm múi giờ nào cũng được; backend đổi sang UTC trước khi lưu.</summary>
    [Required(ErrorMessage = "Phải đặt hạn hoàn thành.")]
    public DateTimeOffset? Deadline { get; init; }

    /// <summary>thành viên nhận task ngay lúc tạo (tùy chọn). Không chọn thì giữ luồng hiện tại.</summary>
    public List<long>? AssigneeUserIds { get; init; }
}

/// <summary>Trường nào để trống thì giữ nguyên.</summary>
public record UpdateTaskRequest
{
    /// <summary>Đổi chủ đề được khi đổi cả chủ đề chiến dịch, hoặc khi task chưa gắn đợt nào.</summary>
    public ScriptDomain? Domain { get; init; }

    [StringLength(1000)]
    public string? Description { get; init; }

    [Range(1, 100_000)]
    public int? TargetQty { get; init; }

    public DateTimeOffset? Deadline { get; init; }

    /// <summary>
    /// Task Manager đổi trạng thái nhiệm vụ bằng tay. Gửi `InProgress` để mở
    /// lại một task đã xong, `Completed` để chốt sớm, `Cancelled` để dừng (giống nút Huỷ:
    /// mọi lượt giao đang hoạt động cũng chuyển sang `cancelled`).
    ///
    /// Giá trị này được giữ nguyên cho tới khi có việc thật xảy ra (nộp bản ghi, duyệt,
    /// đổi chỉ tiêu…) — lúc đó `TaskStateMachine` tính lại và có thể ghi đè. Đổi sang
    /// `InProgress` thì task mở lại ngay, không cần nâng chỉ tiêu nữa.
    /// </summary>
    public WorkTaskStatus? Status { get; init; }
}

/// <summary>Chọn đúng MỘT trong hai cách: gửi danh sách ids, hoặc gửi điều kiện autoFill.</summary>
public record AddTaskItemsRequest
{
    /// <summary>script_id với task thu âm, recording_id với task duyệt.</summary>
    public List<string> Ids { get; init; } = [];

    public AutoFillCriteria? AutoFill { get; init; }
}

public record AutoFillCriteria
{
    [Range(1, 1000, ErrorMessage = "Số mục cần lấy phải từ 1 đến 1000.")]
    public int Count { get; init; }

    /// <summary>Chỉ dùng cho task thu âm.</summary>
    public ScriptDomain? Domain { get; init; }

    /// <summary>Chỉ dùng cho task duyệt — duyệt theo từng người đọc.</summary>
    public long? SpeakerId { get; init; }
}

/// <summary>
/// giao cho NHIỀU người cùng lúc. Chỉ tiêu là của cả nhóm, không nhân theo
/// số người — nên giao 3 người vào task chỉ tiêu 100 vẫn là 100 câu, chia đều ra.
/// </summary>
public record AssignTaskRequest
{
    /// <summary>Phải có ít nhất một người nhận. Trùng lặp trong danh sách được bỏ qua.</summary>
    [Required(ErrorMessage = "Phải chọn ít nhất một người nhận.")]
    [MinLength(1, ErrorMessage = "Phải chọn ít nhất một người nhận.")]
    public List<long> UserIds { get; init; } = [];
}

/// <summary>
/// Một người nhận trong danh sách giao việc. Để trống <see cref="TargetQty"/> thì backend
/// chia đều phần chỉ tiêu còn lại; gửi số thì đó là quota riêng của người này.
/// </summary>
public record AssignmentQuotaRequest
{
    [Range(1, long.MaxValue, ErrorMessage = "Người nhận không hợp lệ.")]
    public long UserId { get; init; }

    [Range(1, 100_000, ErrorMessage = "Chỉ tiêu mỗi người phải từ 1 đến 100000.")]
    public int? TargetQty { get; init; }
}

/// <summary>
/// Thay toàn bộ danh sách người nhận đang hoạt động của task bằng danh sách mới, kèm quota.
/// Người không còn trong danh sách chuyển sang `reassigned` (giữ lịch sử), người mới được thêm.
/// </summary>
public record SetTaskAssignmentsRequest
{
    [Required(ErrorMessage = "Phải chọn ít nhất một người nhận.")]
    [MinLength(1, ErrorMessage = "Phải chọn ít nhất một người nhận.")]
    public List<AssignmentQuotaRequest> Assignments { get; init; } = [];
}

/// <param name="Unit">Đơn vị của chỉ tiêu: cặp câu hay bản ghi.</param>
/// <param name="Done">Task thu âm: số cặp câu đã đủ hai bản duyệt đạt. Task duyệt: số bản ghi đã duyệt.</param>
/// <param name="Submitted">Task thu âm: số bản ghi đã nộp, không tính bản trượt QC. Task duyệt: số lượt đã duyệt.</param>
public record TaskProgressDto(
    string Unit,
    int TargetQty,
    int Done,
    int Submitted,
    int UsableItems,
    int TotalItems,
    int Percent,
    double? HoursRemaining,
    bool IsOverdue)
{
    /// <summary>
    /// <see cref="Done"/> nhưng không vượt quá chỉ tiêu — dùng để vẽ thanh tiến độ.
    ///
    /// Hệ thống cho làm tiếp sau khi đạt chỉ tiêu (xem WorkAfterTargetTests), nên
    /// <see cref="Done"/> lớn hơn <see cref="TargetQty"/> là chuyện bình thường, không
    /// phải lỗi dữ liệu. Giao diện vẽ "13/2" thì khó hiểu, nên có sẵn bản đã chặn ở đây.
    /// Số thật vẫn nằm nguyên ở <see cref="Done"/> để Task Manager biết nhóm làm dư bao nhiêu.
    /// </summary>
    public int DoneCapped => Math.Min(Done, TargetQty);

    /// <summary><see cref="Submitted"/> nhưng không vượt quá chỉ tiêu. Xem <see cref="DoneCapped"/>.</summary>
    public int SubmittedCapped => Math.Min(Submitted, TargetQty);
}

/// <summary>Gắn task vào chiến dịch (attach/move), hoặc gỡ ra (detach) khi CampaignId = null.</summary>
public record AttachTaskRequest
{
    [Range(1, long.MaxValue, ErrorMessage = "Chiến dịch không hợp lệ.")]
    public long? CampaignId { get; init; }
}

/// <summary>một người đang nhận task. Trước đây chỉ có một assigneeId/assigneeName.</summary>
/// <param name="TargetQty">Chỉ tiêu riêng của người này; null với lượt giao cũ chưa có quota.</param>
public record TaskAssigneeDto(long UserId, string FullName, int? TargetQty);

/// <param name="CanDelete">Task rảnh (chưa gắn đợt, chưa có ai nhận, chưa có mục) thì xoá cứng được.</param>
/// <param name="DeleteBlockedReason">Vì sao không xoá được — tiếng Việt để frontend hiện thẳng cho người dùng.</param>
public record TaskListItemDto(
    long TaskId,
    long? CampaignId,
    string? CampaignName,
    TaskType TaskType,
    ScriptDomain Domain,
    string? Description,
    WorkTaskStatus Status,
    IReadOnlyList<TaskAssigneeDto> Assignees,
    DateTimeOffset? Deadline,
    DateTimeOffset CreatedAt,
    TaskProgressDto Progress,
    bool CanDelete,
    string? DeleteBlockedReason);

public record TaskAssignmentDto(
    long AssignmentId,
    long UserId,
    string FullName,
    int? TargetQty,
    AssignmentStatus Status,
    DateTimeOffset AssignedAt);

/// <param name="Id">script_id với task thu âm, recording_id với task duyệt.</param>
/// <param name="AssigneeUserId">Speaker được gán cặp câu này; null = chưa gán hoặc task duyệt.</param>
public record TaskItemDto(
    string Id,
    string Status,
    string? Preview,
    DateTimeOffset IncludedAt,
    long? AssigneeUserId = null);

/// <param name="Assignments">Toàn bộ lịch sử giao việc, kể cả các lượt đã chuyển cho người khác.</param>
public record TaskDetailDto(
    TaskListItemDto Summary,
    IReadOnlyList<TaskAssignmentDto> Assignments,
    IReadOnlyList<TaskItemDto> Items);

public record SkippedItemDto(string Id, string Reason);

public record AddTaskItemsResult(int Added, IReadOnlyList<SkippedItemDto> Skipped, TaskDetailDto Task);

public record TaskSearchRequest : PageRequest
{
    public TaskType? TaskType { get; init; }
    public WorkTaskStatus? Status { get; init; }
    public long? AssigneeId { get; init; }

    /// <summary>Lọc theo chủ đề.</summary>
    public ScriptDomain? Domain { get; init; }

    /// <summary>true: chỉ task còn đang làm mà đã quá hạn.</summary>
    public bool? Overdue { get; init; }
}

public record AssigneeSummaryDto(
    long UserId,
    string FullName,
    string Role,
    int ActiveTasks,
    int TotalTarget,
    int TotalDone,
    int Percent,
    int OverdueTasks);

/// <param name="TaskTitle">Tên nhiệm vụ (task.description) — cùng nghĩa với `taskTitle` ở lịch sử ghi âm.</param>
/// <param name="MyProgress">tiến độ của riêng người đang xem, tính trên quota của người đó.</param>
/// <param name="TeamProgress">tiến độ của cả nhóm người nhận task.</param>
/// <param name="Assignees">ai cùng nhận task này.</param>
public record SpeakerActiveTaskDto(
    long TaskId,
    string? TaskTitle,
    string? Description,
    string? CampaignName,
    TaskType TaskType,
    WorkTaskStatus Status,
    DateTimeOffset? Deadline,
    TaskProgressDto MyProgress,
    TaskProgressDto TeamProgress,
    IReadOnlyList<TaskAssigneeDto> Assignees);

public record SpeakerProgressDto(
    int TotalSubmitted,
    int TotalApproved,
    IReadOnlyList<SpeakerActiveTaskDto> ActiveTasks);

/// <summary>Tổng quan cho trang chủ Task Manager: chiến dịch của mình + tải của từng người nhận.</summary>
/// <param name="ActiveRecordingTasks">đổi tên từ `OpenRecordingTasks` — trạng thái
/// `Open` của task đã bị bỏ, "đang chạy" giờ chỉ có một giá trị là `InProgress`.</param>
public record TaskManagerOverviewDto(
    IReadOnlyList<CampaignProgressDto> MyCampaigns,
    IReadOnlyList<AssigneeSummaryDto> ByAssignee,
    int ActiveRecordingTasks,
    int ActiveReviewTasks,
    int OverdueTasks);
