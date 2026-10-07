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

    /// <summary>Giao việc. Task đang có người nhận thì đây chính là điều phối lại.</summary>
    Task<TaskDetailDto> AssignAsync(
        long taskId, long userId, CancellationToken ct = default,
        long? callerId = null, bool isAdmin = false);

    Task<TaskDetailDto> CancelAsync(
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
