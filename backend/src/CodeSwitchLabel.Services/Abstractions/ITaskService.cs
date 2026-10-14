using CodeSwitchLabel.Repositories.Enums;
using CodeSwitchLabel.Services.Dtos;

namespace CodeSwitchLabel.Services.Abstractions;

public interface ITaskService
{
    Task<TaskDetailDto> CreateAsync(CreateTaskRequest request, long createdById, CancellationToken ct = default);

    Task<PagedResult<TaskListItemDto>> SearchAsync(TaskSearchRequest request, CancellationToken ct = default);

    Task<TaskDetailDto> GetAsync(long taskId, CancellationToken ct = default);

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
    /// TEAM_010: giao cho NHIỀU người cùng lúc — giao thêm, không đuổi người đang nhận.
    /// Chỉ tiêu là của cả nhóm nên không nhân theo số người.
    /// </summary>
    Task<TaskDetailDto> AssignAsync(
        long taskId, long[] userIds, CancellationToken ct = default,
        long? callerId = null, bool isAdmin = false);

    /// <summary>TEAM_010: gỡ một người khỏi task, giữ lịch sử lượt giao.</summary>
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

    /// <summary>Gỡ task khỏi chiến dịch nhưng giữ lại task (C2).</summary>
    Task<TaskDetailDto> DetachAsync(
        long campaignId, long taskId, CancellationToken ct = default,
        long? callerId = null, bool isAdmin = false);

    // TEAM_003: Xoá cứng task rảnh (độc lập + chưa có việc).
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
