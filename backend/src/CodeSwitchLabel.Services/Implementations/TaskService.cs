using CodeSwitchLabel.Repositories.Entities;
using CodeSwitchLabel.Repositories.Enums;
using CodeSwitchLabel.Repositories.Persistence;
using CodeSwitchLabel.Repositories.Repositories;
using CodeSwitchLabel.Services.Abstractions;
using CodeSwitchLabel.Services.Common;
using CodeSwitchLabel.Services.Dtos;
using CodeSwitchLabel.Services.Reviews;
using CodeSwitchLabel.Services.WorkTasks;

namespace CodeSwitchLabel.Services.Implementations;

internal static class TaskMapper
{
    public static TaskListItemDto ToListItem(this TaskRow r, DateTimeOffset now) =>
        new(r.TaskId,
            r.TaskType,
            r.Description,
            r.Status,
            r.AssigneeId,
            r.AssigneeName,
            r.Deadline,
            r.CreatedAt,
            new TaskProgressDto(
                r.TaskType == TaskType.Recording ? "cặp câu" : "bản ghi",
                r.TargetQty,
                r.Done,
                r.Started,
                r.UsableItems,
                r.TotalItems,
                TaskStateMachine.Percent(r.Done, r.TargetQty),
                r.Deadline.HasValue ? Math.Round((r.Deadline.Value - now).TotalHours, 1) : null,
                r.Deadline.HasValue && r.Deadline.Value < now && TaskStateMachine.AcceptsWork(r.Status)));
}

public class TaskService(
    ITaskRepository tasks,
    ITaskProgressTracker tracker,
    ISystemConfigService config,
    TimeProvider clock) : ITaskService
{
    public async Task<TaskDetailDto> CreateAsync(
        CreateTaskRequest request, long createdById, CancellationToken ct = default)
    {
        var now = clock.GetUtcNow();

        var task = new WorkTask
        {
            CreatedBy = createdById,
            TaskType = request.TaskType!.Value,
            Description = Clean(request.Description),
            TargetQty = request.TargetQty!.Value,
            Deadline = ToUtcFuture(request.Deadline!.Value, now),
            Status = WorkTaskStatus.Draft,
            CreatedAt = now
        };

        tasks.Add(task);
        await tasks.SaveChangesAsync(ct);

        return await LoadDetailAsync(task.TaskId, ct);
    }

    public async Task<PagedResult<TaskListItemDto>> SearchAsync(
        TaskSearchRequest request, CancellationToken ct = default)
    {
        var now = clock.GetUtcNow();

        var (rows, total) = await tasks.SearchAsync(
            request.TaskType, request.Status, request.AssigneeId, request.Overdue,
            now, request.Page, request.PageSize, ct);

        return new PagedResult<TaskListItemDto>(
            [.. rows.Select(r => r.ToListItem(now))], request.Page, request.PageSize, total);
    }

    public Task<TaskDetailDto> GetAsync(long taskId, CancellationToken ct = default) =>
        LoadDetailAsync(taskId, ct);

    public async Task<TaskDetailDto> UpdateAsync(
        long taskId, UpdateTaskRequest request, CancellationToken ct = default)
    {
        var task = await LoadEditableAsync(taskId, ct);

        if (request.Description is not null) task.Description = Clean(request.Description);

        if (request.Deadline.HasValue) task.Deadline = ToUtcFuture(request.Deadline.Value, clock.GetUtcNow());

        if (request.TargetQty.HasValue)
        {
            // Task còn nháp thì cứ để Task Manager đặt chỉ tiêu trước, thêm mục sau.
            if (task.Status != WorkTaskStatus.Draft)
            {
                var assigneeId = (await tasks.GetActiveAssignmentAsync(taskId, ct))?.UserId;
                var (workable, blocked) = await CountWorkableAsync(task, assigneeId, null, ct);

                if (request.TargetQty.Value > workable)
                {
                    throw new UnprocessableException(
                        "target_exceeds_items",
                        DescribeShortfall(request.TargetQty.Value, workable, blocked) + " Thêm mục hoặc hạ chỉ tiêu.");
                }
            }

            task.TargetQty = request.TargetQty.Value;
        }

        await tasks.SaveChangesAsync(ct);

        // Đổi chỉ tiêu có thể làm task xong ngay, hoặc mở lại một task đã xong.
        await tracker.RefreshStatusAsync(taskId, ct);

        return await LoadDetailAsync(taskId, ct);
    }

    public async Task<AddTaskItemsResult> AddItemsAsync(
        long taskId, AddTaskItemsRequest request, CancellationToken ct = default)
    {
        var task = await LoadEditableAsync(taskId, ct);

        var hasIds = request.Ids.Count > 0;
        var hasAutoFill = request.AutoFill is not null;

        if (hasIds == hasAutoFill)
        {
            throw new UnprocessableException(
                "items_source_required",
                "Chọn đúng một cách: gửi danh sách ids, hoặc gửi điều kiện autoFill.");
        }

        var assigneeId = (await tasks.GetActiveAssignmentAsync(taskId, ct))?.UserId;
        var skipped = new List<SkippedItemDto>();
        var now = clock.GetUtcNow();
        List<string> accepted;

        if (task.TaskType == TaskType.Recording)
        {
            string[] ids = hasIds
                ? [.. request.Ids.Distinct()]
                : [.. await tasks.FindScriptCandidatesAsync(
                    taskId, assigneeId, request.AutoFill!.Domain, request.AutoFill.Count, ct)];

            accepted = await FilterScriptsAsync(taskId, ids, assigneeId, skipped, ct);

            tasks.AddScriptItems(accepted.Select(id => new TaskScript
            {
                TaskId = taskId,
                ScriptId = id,
                Status = TaskScriptStatus.Pending,
                IncludedAt = now
            }));
        }
        else
        {
            var roundsRequired = await RoundsRequiredAsync(ct);

            string[] ids = hasIds
                ? [.. request.Ids.Distinct()]
                : [.. await tasks.FindRecordingCandidatesAsync(
                    taskId, assigneeId, request.AutoFill!.SpeakerId, request.AutoFill.Count, roundsRequired, ct)];

            accepted = await FilterRecordingsAsync(taskId, ids, assigneeId, roundsRequired, skipped, ct);

            tasks.AddRecordingItems(accepted.Select(id => new TaskRecording
            {
                TaskId = taskId,
                RecordingId = id,
                Status = TaskRecordingStatus.Queued,
                IncludedAt = now
            }));
        }

        await tasks.SaveChangesAsync(ct);

        return new AddTaskItemsResult(accepted.Count, skipped, await LoadDetailAsync(taskId, ct));
    }

    public async Task<TaskDetailDto> RemoveItemAsync(
        long taskId, string itemId, CancellationToken ct = default)
    {
        var task = await LoadEditableAsync(taskId, ct);

        if (task.TaskType == TaskType.Recording)
        {
            var item = await tasks.GetScriptItemAsync(taskId, itemId, ct)
                       ?? throw ItemNotFound(taskId, itemId);

            if (item.Status != TaskScriptStatus.Pending)
            {
                throw ItemNotRemovable(itemId, item.Status.ToString());
            }

            // Mục vẫn Pending trong lúc bản ghi của nó chờ duyệt — nhìn trạng thái mục thôi chưa đủ.
            if (await tasks.HasSubmittedRecordingAsync(itemId, ct))
            {
                throw new ConflictException(
                    "task_item_not_removable",
                    $"Cặp câu {itemId} đã có bản ghi nộp và đang chờ duyệt, không gỡ được — gỡ đi là mất dấu công việc đã làm.");
            }

            await EnsureRemovalKeepsTargetAsync(task, itemId, ct);
            tasks.Remove(item);
        }
        else
        {
            var item = await tasks.GetRecordingItemAsync(taskId, itemId, ct)
                       ?? throw ItemNotFound(taskId, itemId);

            if (item.Status != TaskRecordingStatus.Queued)
            {
                throw ItemNotRemovable(itemId, item.Status.ToString());
            }

            await EnsureRemovalKeepsTargetAsync(task, itemId, ct);
            tasks.Remove(item);
        }

        await tasks.SaveChangesAsync(ct);
        return await LoadDetailAsync(taskId, ct);
    }

    public async Task<TaskDetailDto> AssignAsync(long taskId, long userId, CancellationToken ct = default)
    {
        var task = await LoadEditableAsync(taskId, ct);

        var user = await tasks.GetUserWithRoleAsync(userId, ct)
                   ?? throw new NotFoundException("user_not_found", $"Không tìm thấy tài khoản #{userId}.");

        if (user.Status != UserStatus.Active)
        {
            throw new UnprocessableException("user_inactive", $"Tài khoản {user.Email} đang bị vô hiệu hoá.");
        }

        var requiredRole = task.TaskType == TaskType.Recording ? RoleName.Speaker : RoleName.Reviewer;

        if (user.Role.RoleName != requiredRole)
        {
            throw new UnprocessableException(
                "assignee_role_mismatch",
                $"Task {task.TaskType} chỉ giao được cho {requiredRole}, còn {user.Email} là {user.Role.RoleName}.");
        }

        // So với số mục CHÍNH NGƯỜI NÀY làm được, không phải tổng số mục: Task Manager phải lấp mục
        // trước rồi mới giao được, nên lúc tự lấp hệ thống chưa biết ai sẽ nhận.
        var (workable, blocked) = await CountWorkableAsync(task, userId, null, ct);

        if (task.TargetQty > workable)
        {
            throw new UnprocessableException(
                "target_exceeds_items",
                DescribeShortfall(task.TargetQty, workable, blocked) + " Thêm mục, gỡ các mục đó, hoặc hạ chỉ tiêu.");
        }

        await using var transaction = await tasks.BeginTransactionAsync(ct);

        var current = await tasks.GetActiveAssignmentAsync(taskId, ct);

        if (current?.UserId == userId)
        {
            throw new ConflictException("already_assigned", $"Task #{taskId} đang được giao cho chính người này.");
        }

        if (current is not null)
        {
            // Lượt giao cũ không bị xoá — chuyển sang reassigned để còn lịch sử ai từng nhận task.
            current.AssignmentStatus = AssignmentStatus.Reassigned;

            // Lưu NGAY: database chỉ cho mỗi task một lượt giao đang hoạt động, gộp một lần lưu
            // thì EF có thể ghi lượt mới trước và vi phạm index duy nhất.
            await tasks.SaveChangesAsync(ct);
        }

        tasks.AddAssignment(new TaskAssignment
        {
            TaskId = taskId,
            UserId = userId,
            AssignedAt = clock.GetUtcNow(),
            AssignmentStatus = AssignmentStatus.Active
        });

        await tasks.SaveChangesAsync(ct);

        // Giao lần đầu: Draft → Open. Điều phối lại task đã có việc: giữ InProgress.
        await tracker.RefreshStatusAsync(taskId, ct);
        await transaction.CommitAsync(ct);

        return await LoadDetailAsync(taskId, ct);
    }

    public async Task<TaskDetailDto> CancelAsync(long taskId, CancellationToken ct = default)
    {
        var task = await LoadEditableAsync(taskId, ct);

        if (task.Status == WorkTaskStatus.Completed)
        {
            throw new ConflictException("task_completed", $"Task #{taskId} đã hoàn thành, không huỷ được.");
        }

        task.Status = WorkTaskStatus.Cancelled;

        var current = await tasks.GetActiveAssignmentAsync(taskId, ct);
        if (current is not null) current.AssignmentStatus = AssignmentStatus.Cancelled;

        // Các mục giữ nguyên để còn dấu vết. Task đã huỷ không giữ chỗ gì nữa:
        // truy vấn tự lấp chỉ tránh những mục đang nằm trong task còn chạy.
        await tasks.SaveChangesAsync(ct);

        return await LoadDetailAsync(taskId, ct);
    }

    public async Task<IReadOnlyList<AssigneeSummaryDto>> GetAssigneeSummaryAsync(CancellationToken ct = default)
    {
        var rows = await tasks.GetAssigneeSummaryAsync(clock.GetUtcNow(), ct);

        return [.. rows.Select(r => new AssigneeSummaryDto(
            r.UserId,
            r.FullName,
            r.Role.ToString(),
            r.ActiveTasks,
            r.TotalTarget,
            r.TotalDone,
            TaskStateMachine.Percent(r.TotalDone, r.TotalTarget),
            r.OverdueTasks))];
    }

    public async Task<SpeakerProgressDto> GetSpeakerProgressAsync(
        long speakerId, CancellationToken ct = default)
    {
        var now = clock.GetUtcNow();
        var rows = await tasks.GetActiveRowsForAssigneeAsync(speakerId, TaskType.Recording, ct);
        var (submitted, approved) = await tasks.GetSpeakerTotalsAsync(speakerId, ct);

        return new SpeakerProgressDto(submitted, approved, [.. rows.Select(r => r.ToListItem(now))]);
    }

    public async Task<IReadOnlyList<AssignableUserDto>> GetAssignableUsersAsync(
        TaskType taskType, CancellationToken ct = default)
    {
        var role = taskType == TaskType.Recording ? RoleName.Speaker : RoleName.Reviewer;
        var rows = await tasks.GetAssignableUsersAsync(role, ct);

        return [.. rows.Select(r => new AssignableUserDto(r.UserId, r.FullName, r.Role, r.ActiveTasks, r.TotalTarget))];
    }

    // ----------------------------------------------------------------- nội bộ

    private async Task<List<string>> FilterScriptsAsync(
        long taskId, string[] ids, long? assigneeId, List<SkippedItemDto> skipped, CancellationToken ct)
    {
        var existing = (await tasks.GetScriptItemsAsync(taskId, ct)).Select(i => i.ScriptId).ToHashSet();
        var found = (await tasks.GetScriptsAsync(ids, ct)).ToDictionary(s => s.ScriptId);
        var accepted = new List<string>();

        foreach (var id in ids)
        {
            if (existing.Contains(id))
                skipped.Add(new SkippedItemDto(id, "Đã có trong task."));
            else if (!found.TryGetValue(id, out var script))
                skipped.Add(new SkippedItemDto(id, "Không tồn tại."));
            else if (script.Status != ScriptStatus.Validated)
                skipped.Add(new SkippedItemDto(id, $"Đang ở trạng thái {script.Status}, chỉ nhận câu đã duyệt nội dung."));
            else if (assigneeId.HasValue && script.Recordings.Any(r => r.SpeakerId != assigneeId.Value))
                skipped.Add(new SkippedItemDto(id, "Cặp câu này do người đọc khác thu."));
            else
                accepted.Add(id);
        }

        return accepted;
    }

    private async Task<List<string>> FilterRecordingsAsync(
        long taskId, string[] ids, long? assigneeId, int roundsRequired,
        List<SkippedItemDto> skipped, CancellationToken ct)
    {
        var existing = (await tasks.GetRecordingItemsAsync(taskId, ct)).Select(i => i.RecordingId).ToHashSet();
        var found = (await tasks.GetRecordingsWithReviewsAsync(ids, ct)).ToDictionary(r => r.RecordingId);
        var accepted = new List<string>();

        foreach (var id in ids)
        {
            if (existing.Contains(id))
                skipped.Add(new SkippedItemDto(id, "Đã có trong task."));
            else if (!found.TryGetValue(id, out var recording))
                skipped.Add(new SkippedItemDto(id, "Không tồn tại."));
            else if (recording.Status != RecordingStatus.PendingReview)
                skipped.Add(new SkippedItemDto(id, $"Đang ở trạng thái {recording.Status}, không còn chờ duyệt."));
            else if (recording.Reviews.Count >= roundsRequired)
                skipped.Add(new SkippedItemDto(id, $"Đã đủ {roundsRequired} lượt duyệt."));
            else if (assigneeId.HasValue && recording.SpeakerId == assigneeId.Value)
                skipped.Add(new SkippedItemDto(id, "Người nhận task chính là người thu bản ghi này."));
            else if (assigneeId.HasValue && recording.Reviews.Any(v => v.ReviewerId == assigneeId.Value))
                skipped.Add(new SkippedItemDto(id, "Người nhận task đã duyệt bản ghi này rồi."));
            else
                accepted.Add(id);
        }

        return accepted;
    }

    private async Task EnsureRemovalKeepsTargetAsync(WorkTask task, string itemId, CancellationToken ct)
    {
        if (task.Status == WorkTaskStatus.Draft) return;

        var assigneeId = (await tasks.GetActiveAssignmentAsync(task.TaskId, ct))?.UserId;
        var (workable, blocked) = await CountWorkableAsync(task, assigneeId, itemId, ct);

        if (task.TargetQty > workable)
        {
            throw new ConflictException(
                "target_exceeds_items",
                $"Gỡ mục {itemId} thì task không còn đủ mục. " +
                DescribeShortfall(task.TargetQty, workable, blocked) + " Hạ chỉ tiêu trước.");
        }
    }

    /// <summary>
    /// Số mục người nhận còn làm được: bỏ mục đã loại, và bỏ mục chính người đó không làm được nữa —
    /// cặp câu của người khác, hoặc bản ghi do chính họ thu.
    /// </summary>
    private async Task<(int Workable, List<string> Blocked)> CountWorkableAsync(
        WorkTask task, long? assigneeId, string? excludingItemId, CancellationToken ct)
    {
        var row = await tasks.GetRowAsync(task.TaskId, ct) ?? throw NotFound(task.TaskId);

        List<string> blocked = assigneeId.HasValue
            ? await tasks.GetBlockedItemIdsAsync(task.TaskId, assigneeId.Value, task.TaskType, ct)
            : [];

        var usable = row.UsableItems;

        if (excludingItemId is not null)
        {
            usable -= 1;
            blocked.Remove(excludingItemId);
        }

        return (usable - blocked.Count, blocked);
    }

    private static string DescribeShortfall(int target, int workable, List<string> blocked)
    {
        var message = $"Chỉ tiêu {target} vượt quá {Math.Max(workable, 0)} mục người nhận làm được.";

        if (blocked.Count > 0)
        {
            message += $" Có {blocked.Count} mục người nhận không làm được: {string.Join(", ", blocked)}.";
        }

        return message;
    }

    private Task<int> RoundsRequiredAsync(CancellationToken ct) =>
        config.GetIntAsync(ConfigKeys.ReviewRoundsRequired, ReviewRules.RoundsRequiredInDatabase, ct);

    private async Task<WorkTask> LoadEditableAsync(long taskId, CancellationToken ct)
    {
        var task = await tasks.GetForUpdateAsync(taskId, ct) ?? throw NotFound(taskId);

        if (task.Status == WorkTaskStatus.Cancelled)
        {
            throw new ConflictException("task_cancelled", $"Task #{taskId} đã bị huỷ, không sửa được nữa.");
        }

        return task;
    }

    private async Task<TaskDetailDto> LoadDetailAsync(long taskId, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var row = await tasks.GetRowAsync(taskId, ct) ?? throw NotFound(taskId);
        var history = await tasks.GetAssignmentHistoryAsync(taskId, ct);

        IReadOnlyList<TaskItemDto> items = row.TaskType == TaskType.Recording
            ? [.. (await tasks.GetScriptItemsAsync(taskId, ct)).Select(i =>
                new TaskItemDto(i.ScriptId, i.Status.ToString(), Preview(i.Script.CsContent), i.IncludedAt))]
            : [.. (await tasks.GetRecordingItemsAsync(taskId, ct)).Select(i =>
                new TaskItemDto(
                    i.RecordingId,
                    i.Status.ToString(),
                    $"{i.Recording.SentenceVariant} · {i.Recording.Status} · {i.Recording.DurationSec:0.##} giây",
                    i.IncludedAt))];

        return new TaskDetailDto(
            row.ToListItem(now),
            [.. history.Select(a => new TaskAssignmentDto(
                a.AssignmentId, a.UserId, a.User.FullName, a.AssignmentStatus, a.AssignedAt))],
            items);
    }

    /// <summary>
    /// Kiểu timestamptz qua Npgsql CHỈ nhận DateTimeOffset có offset 0, nên phải đổi sang UTC.
    /// </summary>
    private static DateTimeOffset ToUtcFuture(DateTimeOffset deadline, DateTimeOffset now)
    {
        var utc = deadline.ToUniversalTime();

        if (utc <= now)
        {
            throw new UnprocessableException("deadline_in_past", "Hạn hoàn thành phải nằm trong tương lai.");
        }

        return utc;
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string Preview(string content)
    {
        string plain;
        try { plain = CodeSwitchText.Strip(content); }
        catch (FormatException) { plain = content; }

        return plain.Length <= 80 ? plain : plain[..77] + "...";
    }

    private static NotFoundException NotFound(long taskId) =>
        new("task_not_found", $"Không tìm thấy task #{taskId}.");

    private static NotFoundException ItemNotFound(long taskId, string itemId) =>
        new("task_item_not_found", $"Mục {itemId} không nằm trong task #{taskId}.");

    private static ConflictException ItemNotRemovable(string itemId, string status) =>
        new("task_item_not_removable",
            $"Mục {itemId} đã ở trạng thái {status}, không gỡ được — gỡ đi là mất dấu công việc đã làm.");
}
