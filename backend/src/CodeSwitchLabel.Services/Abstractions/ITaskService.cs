using CodeSwitchLabel.Services.Dtos;

namespace CodeSwitchLabel.Services.Abstractions;

public interface ITaskService
{
    Task<TaskDetailDto> CreateAsync(CreateTaskRequest request, long createdById, CancellationToken ct = default);

    Task<PagedResult<TaskListItemDto>> SearchAsync(TaskSearchRequest request, CancellationToken ct = default);

    Task<TaskDetailDto> GetAsync(long taskId, CancellationToken ct = default);

    Task<TaskDetailDto> UpdateAsync(long taskId, UpdateTaskRequest request, CancellationToken ct = default);

    Task<AddTaskItemsResult> AddItemsAsync(long taskId, AddTaskItemsRequest request, CancellationToken ct = default);

    Task<TaskDetailDto> RemoveItemAsync(long taskId, string itemId, CancellationToken ct = default);

    /// <summary>Giao việc. Task đang có người nhận thì đây chính là điều phối lại.</summary>
    Task<TaskDetailDto> AssignAsync(long taskId, long userId, CancellationToken ct = default);

    Task<TaskDetailDto> CancelAsync(long taskId, CancellationToken ct = default);

    Task<IReadOnlyList<AssigneeSummaryDto>> GetAssigneeSummaryAsync(CancellationToken ct = default);

    Task<SpeakerProgressDto> GetSpeakerProgressAsync(long speakerId, CancellationToken ct = default);
}
