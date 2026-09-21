using System.ComponentModel.DataAnnotations;
using CodeSwitchLabel.Repositories.Enums;

namespace CodeSwitchLabel.Services.Dtos;

public record CreateTaskRequest
{
    [Required(ErrorMessage = "Phải chọn loại task: Recording hoặc Review.")]
    public TaskType? TaskType { get; init; }

    [StringLength(1000)]
    public string? Description { get; init; }

    /// <summary>Task thu âm: số CẶP CÂU. Task duyệt: số bản ghi.</summary>
    [Required(ErrorMessage = "Phải đặt chỉ tiêu.")]
    [Range(1, 100_000)]
    public int? TargetQty { get; init; }

    /// <summary>Gửi kèm múi giờ nào cũng được; backend đổi sang UTC trước khi lưu.</summary>
    [Required(ErrorMessage = "Phải đặt hạn hoàn thành.")]
    public DateTimeOffset? Deadline { get; init; }
}

/// <summary>Trường nào để trống thì giữ nguyên.</summary>
public record UpdateTaskRequest
{
    [StringLength(1000)]
    public string? Description { get; init; }

    [Range(1, 100_000)]
    public int? TargetQty { get; init; }

    public DateTimeOffset? Deadline { get; init; }
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

public record AssignTaskRequest
{
    [Required(ErrorMessage = "Phải chọn người nhận.")]
    public long? UserId { get; init; }
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
    bool IsOverdue);

public record TaskListItemDto(
    long TaskId,
    TaskType TaskType,
    string? Description,
    WorkTaskStatus Status,
    long? AssigneeId,
    string? AssigneeName,
    DateTimeOffset? Deadline,
    DateTimeOffset CreatedAt,
    TaskProgressDto Progress);

public record TaskAssignmentDto(
    long AssignmentId, long UserId, string FullName, AssignmentStatus Status, DateTimeOffset AssignedAt);

/// <param name="Id">script_id với task thu âm, recording_id với task duyệt.</param>
public record TaskItemDto(string Id, string Status, string? Preview, DateTimeOffset IncludedAt);

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

public record SpeakerProgressDto(
    int TotalSubmitted,
    int TotalApproved,
    IReadOnlyList<TaskListItemDto> ActiveTasks);
