using CodeSwitchLabel.Repositories.Enums;
using CodeSwitchLabel.Services.Dtos;

namespace CodeSwitchLabel.Services.Abstractions;

public interface ITaskService
{
    Task<TaskDetailDto> CreateAsync(CreateTaskRequest request, long createdById, CancellationToken ct = default);

    Task<PagedResult<TaskListItemDto>> SearchAsync(TaskSearchRequest request, CancellationToken ct = default);

    Task<TaskDetailDto> GetAsync(long taskId, CancellationToken ct = default);

    /// <summary>
    /// Lấy (hoặc tạo idempotent) task thu âm duy nhất của chiến dịch, chỉ tiêu bằng cả chiến dịch.
    /// <paramref name="autoFill"/> tự lấp cặp câu đã duyệt nội dung cho tới khi đủ chỉ tiêu.
    /// </summary>
    Task<TaskDetailDto> GetOrCreateRecordingTaskAsync(
        long campaignId, bool autoFill, long callerId, bool isAdmin, CancellationToken ct = default);

    Task<TaskDetailDto> UpdateAsync(
        long taskId, UpdateTaskRequest request, CancellationToken ct = default,
        long? callerId = null, bool isAdmin = false);

    Task<AddTaskItemsResult> AddItemsAsync(
        long taskId, AddTaskItemsRequest request, CancellationToken ct = default,
        long? callerId = null, bool isAdmin = false);

    Task<TaskDetailDto> RemoveItemAsync(
        long taskId, string itemId, CancellationToken ct = default,
        long? callerId = null, bool isAdmin = false);

    /// <summary>
    /// giao cho NHIỀU người cùng lúc — giao thêm, không đuổi người đang nhận.
    /// Chỉ tiêu là của cả nhóm nên không nhân theo số người.
    /// </summary>
    Task<TaskDetailDto> AssignAsync(
        long taskId, long[] userIds, CancellationToken ct = default,
        long? callerId = null, bool isAdmin = false);

    /// <summary>
    /// Thay toàn bộ người nhận đang hoạt động bằng danh sách mới, kèm quota từng người.
    /// Ô quota trống được chia đều phần chỉ tiêu còn lại.
    /// </summary>
    Task<TaskDetailDto> SetAssignmentsAsync(
        long taskId, SetTaskAssignmentsRequest request, CancellationToken ct = default,
        long? callerId = null, bool isAdmin = false);

    /// <summary>gỡ một người khỏi task, giữ lịch sử lượt giao.</summary>
    Task<TaskDetailDto> UnassignAsync(
        long taskId, long userId, CancellationToken ct = default,
        long? callerId = null, bool isAdmin = false);

    Task<TaskDetailDto> CancelAsync(
        long taskId, CancellationToken ct = default,
        long? callerId = null, bool isAdmin = false);

    /// <summary>Gắn task vào chiến dịch (attach/move), hoặc gỡ ra (detach) khi campaignId = null.</summary>
    Task<TaskDetailDto> AttachAsync(
        long taskId, long? campaignId, CancellationToken ct = default,
        long? callerId = null, bool isAdmin = false);

    /// <summary>Xoá vĩnh viễn task khỏi chiến dịch; bản ghi thu âm và lượt duyệt được giữ lại.</summary>
    Task DeleteFromCampaignAsync(
        long campaignId, long taskId, CancellationToken ct = default,
        long? callerId = null, bool isAdmin = false);

    // Xoá cứng task rảnh (độc lập + chưa có việc).
    /// <summary>
    /// Xoá cứng một nhiệm vụ. Chỉ xoá được task rảnh (chưa gắn đợt nào và chưa có việc/lượt giao/mục nào).
    /// </summary>
    Task DeleteAsync(
        long taskId, CancellationToken ct = default,
        long? callerId = null, bool isAdmin = false);

    Task<IReadOnlyList<AssigneeSummaryDto>> GetAssigneeSummaryAsync(CancellationToken ct = default);

    Task<SpeakerProgressDto> GetSpeakerProgressAsync(long speakerId, CancellationToken ct = default);

    /// <summary>Người giao được loại task này, kèm khối lượng đang gánh.</summary>
    Task<IReadOnlyList<AssignableUserDto>> GetAssignableUsersAsync(TaskType taskType, CancellationToken ct = default);

    /// <summary>Tổng quan trang chủ Task Manager: chiến dịch phụ trách + tải từng người nhận.</summary>
    Task<TaskManagerOverviewDto> GetManagerOverviewAsync(
        long managerId, bool isAdmin, CancellationToken ct = default);
}
