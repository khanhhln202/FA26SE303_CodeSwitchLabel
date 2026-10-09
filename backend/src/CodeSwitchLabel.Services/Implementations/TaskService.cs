using CodeSwitchLabel.Repositories.Entities;
using CodeSwitchLabel.Repositories.Enums;
using CodeSwitchLabel.Repositories.Persistence;
using CodeSwitchLabel.Repositories.Repositories;
using CodeSwitchLabel.Services.Abstractions;
using CodeSwitchLabel.Services.Common;
using CodeSwitchLabel.Services.Dtos;
using CodeSwitchLabel.Services.Reviews;
using CodeSwitchLabel.Services.WorkTasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace CodeSwitchLabel.Services.Implementations;

internal static class TaskMapper
{
    public static TaskListItemDto ToListItem(this TaskRow r, DateTimeOffset now) =>
        new(r.TaskId,
            r.CampaignId,
            r.CampaignName,
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
    ICampaignRepository campaigns,
    ITaskProgressTracker tracker,
    ISystemConfigService config,
    TimeProvider clock) : ITaskService
{
    public async Task<TaskDetailDto> CreateAsync(
        CreateTaskRequest request, long createdById, CancellationToken ct = default)
    {
        var now = clock.GetUtcNow();

        var targetQty = request.TargetQty!.Value;
        var deadline = ToUtcFuture(request.Deadline!.Value, now);

        // campaignId null = task độc lập (C1): bỏ qua mọi kiểm tra chiến dịch.
        // Trigger trg_task_campaign_window / trg_task_campaign_target / trg_task_creator_assigned
        // đều RETURN NEW ngay khi NEW.campaign_id IS NULL.
        Campaign? campaign = null;
        if (request.CampaignId.HasValue)
        {
            var campaignId = request.CampaignId.Value;

            campaign = await campaigns.GetAsync(campaignId, ct)
                ?? throw new NotFoundException("campaign_not_found", $"Không tìm thấy chiến dịch #{campaignId}.");

            // TEAM_001: ERD — Task Manager chỉ tạo được task trong chiến dịch Admin đã giao cho chính
            // mình; chiến dịch chưa giao thì chưa nhận task. Trigger trg_task_creator_assigned của
            // database chặn lần nữa ở tầng sâu.
            if (campaign.AssignedTo != createdById)
            {
                throw new UnprocessableException(
                    "campaign_not_assigned_to_manager",
                    campaign.AssignedTo is null
                        ? $"Chiến dịch #{campaignId} chưa được giao cho Task Manager nào nên chưa nhận task. Nhờ Admin giao chiến dịch trước."
                        : $"Chiến dịch #{campaignId} đang giao cho người khác — bạn chỉ tạo được task trong chiến dịch của mình.");
            }

            // Chiến dịch đã huỷ hoặc đã hoàn thành thì không nhận thêm việc nữa: đóng chiến dịch mà task vẫn
            // chui vào được thì con số chỉ tiêu và báo cáo cuối đợt không còn đúng. Task đã tạo trước đó vẫn chạy.
            if (campaign.Status is CampaignStatus.Completed or CampaignStatus.Cancelled)
            {
                throw new UnprocessableException(
                    "campaign_not_accepting_tasks",
                    $"Chiến dịch #{campaignId} đang ở trạng thái {campaign.Status} nên không nhận thêm task. " +
                    "Mở lại chiến dịch hoặc chọn chiến dịch khác.");
            }

            // Hạn task phải nằm trong khoảng ngày của chiến dịch (trigger của database cũng chặn).
            var deadlineDate = DateOnly.FromDateTime(deadline.UtcDateTime);

            if (deadlineDate < campaign.StartDate || deadlineDate > campaign.EndDate)
            {
                throw new UnprocessableException(
                    "task_deadline_outside_campaign",
                    $"Hạn {deadlineDate:yyyy-MM-dd} phải nằm trong khoảng ngày của chiến dịch " +
                    $"{campaign.StartDate:yyyy-MM-dd} đến {campaign.EndDate:yyyy-MM-dd}.");
            }

            // Tổng chỉ tiêu các task không được vượt chỉ tiêu chiến dịch.
            var allocated = await campaigns.SumAllocatedAsync(campaignId, ct);

            if (allocated + targetQty > campaign.TargetQty)
            {
                throw new UnprocessableException(
                    "task_target_exceeds_campaign",
                    $"Chiến dịch #{campaignId} đã chia {allocated}/{campaign.TargetQty} cặp câu; " +
                    $"thêm {targetQty} là vượt chỉ tiêu.");
            }
        }

        var task = new WorkTask
        {
            CampaignId = request.CampaignId,
            CreatedBy = createdById,
            TaskType = request.TaskType!.Value,
            Description = Clean(request.Description),
            TargetQty = targetQty,
            Deadline = deadline,
            Status = WorkTaskStatus.Draft,
            CreatedAt = now
        };

        tasks.Add(task);
        await SaveTasksAsync(ct);

        return await LoadDetailAsync(task.TaskId, ct);
    }

    /// <summary>Gắn task vào chiến dịch (attach/move), hoặc gỡ ra (detach) khi campaignId = null.</summary>
    public async Task<TaskDetailDto> AttachAsync(
        long taskId, long? campaignId, CancellationToken ct = default,
        long? callerId = null, bool isAdmin = false)
    {
        await using var transaction = await BeginTransactionIfNeededAsync(ct);
        var task = await LoadEditableAsync(taskId, ct);
        await EnsureCampaignOwnerAsync(task, callerId, isAdmin, ct);

        if (campaignId.HasValue)
        {
            var campaign = await campaigns.GetAsync(campaignId.Value, ct)
                ?? throw new NotFoundException("campaign_not_found", $"Không tìm thấy chiến dịch #{campaignId}.");

            if (campaign.AssignedTo.HasValue && callerId.HasValue && !isAdmin
                && campaign.AssignedTo.Value != callerId.Value)
            {
                throw new ForbiddenException(
                    "task_not_owned",
                    $"Chiến dịch #{campaignId} không giao cho bạn nên không gắn task vào được.");
            }

            if (campaign.Status is CampaignStatus.Completed or CampaignStatus.Cancelled)
            {
                throw new UnprocessableException(
                    "campaign_not_accepting_tasks",
                    $"Chiến dịch #{campaignId} đang ở trạng thái {campaign.Status} nên không nhận thêm task.");
            }

            if (task.Deadline.HasValue)
            {
                var deadlineDate = DateOnly.FromDateTime(task.Deadline.Value.UtcDateTime);
                if (deadlineDate < campaign.StartDate || deadlineDate > campaign.EndDate)
                {
                    throw new UnprocessableException(
                        "task_deadline_outside_campaign",
                        $"Hạn {deadlineDate:yyyy-MM-dd} phải nằm trong khoảng ngày của chiến dịch " +
                        $"{campaign.StartDate:yyyy-MM-dd} đến {campaign.EndDate:yyyy-MM-dd}.");
                }
            }

            var allocated = await campaigns.SumAllocatedAsync(campaignId.Value, ct);
            var withoutThis = task.CampaignId == campaignId.Value ? allocated - task.TargetQty : allocated;
            if (withoutThis + task.TargetQty > campaign.TargetQty)
            {
                throw new UnprocessableException(
                    "task_target_exceeds_campaign",
                    $"Chiến dịch #{campaignId} đã chia {allocated}/{campaign.TargetQty} cặp câu; " +
                    $"gắn task này ({task.TargetQty}) là vượt chỉ tiêu.");
            }
        }

        task.CampaignId = campaignId;
        await SaveTasksAsync(ct);
        if (transaction is not null) await transaction.CommitAsync(ct);

        return await LoadDetailAsync(taskId, ct);
    }

    /// <summary>Gỡ task khỏi chiến dịch nhưng giữ lại task (C2). Không xoá assignments/items.</summary>
    public async Task<TaskDetailDto> DetachAsync(
        long campaignId, long taskId, CancellationToken ct = default,
        long? callerId = null, bool isAdmin = false)
    {
        var task = await tasks.GetForUpdateAsync(taskId, ct) ?? throw NotFound(taskId);

        if (task.CampaignId != campaignId)
        {
            throw new NotFoundException(
                "task_not_found", $"Task #{taskId} không thuộc chiến dịch #{campaignId}.");
        }

        return await AttachAsync(taskId, null, ct, callerId, isAdmin);
    }

    /// <summary>Xoá cứng task (C2). Cascade theo lược đồ; recording/review chỉ SET NULL.</summary>
    public async Task DeleteAsync(
        long taskId, CancellationToken ct = default,
        long? callerId = null, bool isAdmin = false)
    {
        await using var transaction = await BeginTransactionIfNeededAsync(ct);
        var task = await LoadEditableAsync(taskId, ct);
        await EnsureCampaignOwnerAsync(task, callerId, isAdmin, ct);

        await tasks.DeleteAsync(taskId, ct);
        if (transaction is not null) await transaction.CommitAsync(ct);
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
        long taskId, UpdateTaskRequest request, CancellationToken ct = default,
        long? callerId = null, bool isAdmin = false)
    {
        await using var transaction = await BeginTransactionIfNeededAsync(ct);
        var task = await LoadEditableAsync(taskId, ct);
        await EnsureCampaignOwnerAsync(task, callerId, isAdmin, ct);

        if (request.Description is not null) task.Description = Clean(request.Description);

        if (request.Deadline.HasValue)
        {
            var deadline = ToUtcFuture(request.Deadline.Value, clock.GetUtcNow());
            var campaign = task.CampaignId.HasValue
                ? await campaigns.GetAsync(task.CampaignId.Value, ct)
                : null;
            if (campaign is not null)
            {
                var deadlineDate = DateOnly.FromDateTime(deadline.UtcDateTime);
                if (deadlineDate < campaign.StartDate || deadlineDate > campaign.EndDate)
                {
                    throw new UnprocessableException(
                        "task_deadline_outside_campaign",
                        $"Hạn {deadlineDate:yyyy-MM-dd} phải nằm trong khoảng ngày của chiến dịch " +
                        $"{campaign.StartDate:yyyy-MM-dd} đến {campaign.EndDate:yyyy-MM-dd}.");
                }
            }
            task.Deadline = deadline;
        }

        if (request.TargetQty.HasValue)
        {
            // Giữ quota chiến dịch như lúc Create: tổng đã chia (trừ task này) + chỉ tiêu mới <= target.
            // Task độc lập (campaign_id NULL) bỏ qua kiểm tra này; trigger cũng RETURN NEW ngay.
            var campaign = task.CampaignId.HasValue
                ? await campaigns.GetAsync(task.CampaignId.Value, ct)
                : null;
            if (campaign is not null)
            {
                var allocated = await campaigns.SumAllocatedAsync(task.CampaignId!.Value, ct);
                var withoutThis = allocated - task.TargetQty;
                if (withoutThis + request.TargetQty.Value > campaign.TargetQty)
                {
                    throw new UnprocessableException(
                        "task_target_exceeds_campaign",
                        $"Chiến dịch #{campaign.CampaignId} đã chia {allocated}/{campaign.TargetQty} cặp câu; " +
                        $"đổi task này thành {request.TargetQty.Value} là vượt chỉ tiêu.");
                }
            }

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

        await SaveTasksAsync(ct);

        // Đổi chỉ tiêu có thể làm task xong ngay, hoặc mở lại một task đã xong.
        await tracker.RefreshStatusAsync(taskId, ct);
        if (transaction is not null) await transaction.CommitAsync(ct);

        return await LoadDetailAsync(taskId, ct);
    }

    public async Task<AddTaskItemsResult> AddItemsAsync(
        long taskId, AddTaskItemsRequest request, CancellationToken ct = default,
        long? callerId = null, bool isAdmin = false)
    {
        await using var transaction = await BeginTransactionIfNeededAsync(ct);
        var task = await LoadMutableAsync(taskId, ct);
        await EnsureCampaignOwnerAsync(task, callerId, isAdmin, ct);

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

        await SaveTasksAsync(ct);
        await tracker.RefreshStatusAsync(taskId, ct);
        if (transaction is not null) await transaction.CommitAsync(ct);

        return new AddTaskItemsResult(accepted.Count, skipped, await LoadDetailAsync(taskId, ct));
    }

    public async Task<TaskDetailDto> RemoveItemAsync(
        long taskId, string itemId, CancellationToken ct = default,
        long? callerId = null, bool isAdmin = false)
    {
        await using var transaction = await BeginTransactionIfNeededAsync(ct);
        var task = await LoadMutableAsync(taskId, ct);
        await EnsureCampaignOwnerAsync(task, callerId, isAdmin, ct);

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

        await SaveTasksAsync(ct);
        if (transaction is not null) await transaction.CommitAsync(ct);
        return await LoadDetailAsync(taskId, ct);
    }

    public async Task<TaskDetailDto> AssignAsync(
        long taskId, long userId, CancellationToken ct = default,
        long? callerId = null, bool isAdmin = false)
    {
        var task = await LoadMutableAsync(taskId, ct);
        await EnsureCampaignOwnerAsync(task, callerId, isAdmin, ct);

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

        await using var transaction = await BeginTransactionIfNeededAsync(ct);

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
            await SaveTasksAsync(ct);
        }

        tasks.AddAssignment(new TaskAssignment
        {
            TaskId = taskId,
            UserId = userId,
            AssignedAt = clock.GetUtcNow(),
            AssignmentStatus = AssignmentStatus.Active
        });

        await SaveTasksAsync(ct);

        // Giao lần đầu: Draft → Open. Điều phối lại task đã có việc: giữ InProgress.
        await tracker.RefreshStatusAsync(taskId, ct);
        if (transaction is not null) await transaction.CommitAsync(ct);

        return await LoadDetailAsync(taskId, ct);
    }

    public async Task<TaskDetailDto> CancelAsync(
        long taskId, CancellationToken ct = default,
        long? callerId = null, bool isAdmin = false)
    {
        var task = await LoadEditableAsync(taskId, ct);
        await EnsureCampaignOwnerAsync(task, callerId, isAdmin, ct);

        if (task.Status == WorkTaskStatus.Completed)
        {
            throw new ConflictException("task_completed", $"Task #{taskId} đã hoàn thành, không huỷ được.");
        }

        task.Status = WorkTaskStatus.Cancelled;

        var current = await tasks.GetActiveAssignmentAsync(taskId, ct);
        if (current is not null) current.AssignmentStatus = AssignmentStatus.Cancelled;

        // Các mục giữ nguyên để còn dấu vết. Task đã huỷ không giữ chỗ gì nữa:
        // truy vấn tự lấp chỉ tránh những mục đang nằm trong task còn chạy.
        await SaveTasksAsync(ct);

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

        // TEAM_002: rows arrive ranked-first from repository
        // (approval DESC, overdue ASC, load ASC); keep that order for Task Manager UI.
        var rows = await tasks.GetAssignableUsersAsync(role, clock.GetUtcNow(), ct);

        return [.. rows.Select(r => new AssignableUserDto(r.UserId, r.FullName, r.Role, r.ActiveTasks, r.TotalTarget, r.ApprovalRatePct, r.OverdueTasks))];
    }

    public async Task<TaskManagerOverviewDto> GetManagerOverviewAsync(
        long managerId, bool isAdmin, CancellationToken ct = default)
    {
        var now = clock.GetUtcNow();

        var progress = await campaigns.GetProgressAsync(null, ct);

        var mine = progress
            .Where(p => isAdmin || p.AssignedTo == managerId)
            .Select(p => new CampaignProgressDto(
                p.CampaignId, p.CampaignName, p.CampaignTargetQty, p.StartDate, p.EndDate,
                p.CampaignStatus, p.AssignedTo, p.AllocatedTaskQty, p.RemainingTaskQty,
                p.TaskCount, p.CompletedTaskCount))
            .ToList();

        var byAssignee = await GetAssigneeSummaryAsync(ct);

        // Đếm task còn chạy theo loại + quá hạn: chỉ cần Total nên pageSize = 1.
        var (_, openRec) = await tasks.SearchAsync(
            TaskType.Recording, WorkTaskStatus.Open, null, null, now, 1, 1, ct);
        var (_, progRec) = await tasks.SearchAsync(
            TaskType.Recording, WorkTaskStatus.InProgress, null, null, now, 1, 1, ct);
        var (_, openRev) = await tasks.SearchAsync(
            TaskType.Review, WorkTaskStatus.Open, null, null, now, 1, 1, ct);
        var (_, progRev) = await tasks.SearchAsync(
            TaskType.Review, WorkTaskStatus.InProgress, null, null, now, 1, 1, ct);
        var (_, overdue) = await tasks.SearchAsync(
            null, null, null, true, now, 1, 1, ct);

        return new TaskManagerOverviewDto(mine, byAssignee, openRec + progRec, openRev + progRev, overdue);
    }

    // ----------------------------------------------------------------- nội bộ

    private async Task<List<string>> FilterScriptsAsync(
        long taskId, string[] ids, long? assigneeId, List<SkippedItemDto> skipped, CancellationToken ct)
    {
        var existing = (await tasks.GetScriptItemsAsync(taskId, ct)).Select(i => i.ScriptId).ToHashSet();
        var found = (await tasks.GetScriptsAsync(ids, ct)).ToDictionary(s => s.ScriptId);
        var queuedElsewhere = await tasks.FindScriptsQueuedInOtherActiveTasksAsync(ids, taskId, assigneeId, ct);
        var accepted = new List<string>();

        foreach (var id in ids)
        {
            if (existing.Contains(id))
                skipped.Add(new SkippedItemDto(id, "Đã có trong task."));
            else if (!found.TryGetValue(id, out var script))
                skipped.Add(new SkippedItemDto(id, "Không tồn tại."));
            else if (script.Status != ScriptStatus.Validated)
                skipped.Add(new SkippedItemDto(id, $"Đang ở trạng thái {script.Status}, chỉ nhận câu đã duyệt nội dung."));
            else if (assigneeId.HasValue && script.Recordings.Any(r => r.SpeakerId != assigneeId.Value && r.Status != RecordingStatus.QcFailed))
                skipped.Add(new SkippedItemDto(id, "Cặp câu này do người đọc khác thu."));
            else if (queuedElsewhere.Contains(id))
                skipped.Add(new SkippedItemDto(id, "Cặp câu đang nằm trong một task khác đang chạy."));
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
        var queuedElsewhere = await tasks.FindRecordingsQueuedInOtherActiveTasksAsync(ids, taskId, ct);
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
            else if (queuedElsewhere.Contains(id))
                skipped.Add(new SkippedItemDto(id, "Bản ghi đang nằm trong một task duyệt khác đang chạy."));
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

    /// <summary>
    /// Task đã Completed thì không giao/thêm/gỡ mục nữa — muốn làm tiếp thì Task Manager
    /// nâng chỉ tiêu qua PATCH để task tự mở lại. Update và Cancel dùng LoadEditableAsync riêng.
    /// </summary>
    private async Task<WorkTask> LoadMutableAsync(long taskId, CancellationToken ct)
    {
        var task = await LoadEditableAsync(taskId, ct);

        if (task.Status == WorkTaskStatus.Completed)
        {
            throw new ConflictException(
                "task_completed",
                $"Task #{taskId} đã hoàn thành. Muốn thêm việc thì nâng chỉ tiêu trước, task sẽ tự mở lại.");
        }

        return task;
    }

    /// <summary>
    /// Chỉ Task Manager sở hữu chiến dịch (campaign.AssignedTo) hoặc Admin được sửa task.
    /// Task độc lập (campaign_id NULL): chỉ người tạo hoặc Admin được sửa.
    /// callerId null nghĩa là caller cũ (test) — bỏ qua để tương thích ngược.
    /// </summary>
    private async Task EnsureCampaignOwnerAsync(WorkTask task, long? callerId, bool isAdmin, CancellationToken ct)
    {
        if (!callerId.HasValue) return;
        if (isAdmin) return;

        if (!task.CampaignId.HasValue)
        {
            if (task.CreatedBy != callerId.Value)
            {
                throw new ForbiddenException(
                    "task_not_owned",
                    $"Task #{task.TaskId} là task độc lập do người khác tạo nên bạn không sửa được.");
            }
            return;
        }

        var campaign = await campaigns.GetAsync(task.CampaignId.Value, ct)
            ?? throw new NotFoundException("campaign_not_found", $"Không tìm thấy chiến dịch #{task.CampaignId}.");

        if (campaign.AssignedTo != callerId.Value)
        {
            throw new ForbiddenException(
                "task_not_owned",
                $"Task #{task.TaskId} thuộc chiến dịch #{campaign.CampaignId} không giao cho bạn.");
        }
    }

    /// <summary>
    /// Mở transaction riêng, trừ khi caller đã ở trong một transaction (test
    /// tích hợp giữ transaction ambient để rollback). EF không cho transaction
    /// lồng nhau trên cùng connection nên trường hợp đó dùng luôn ambient.
    /// </summary>
    private async Task<IDbContextTransaction?> BeginTransactionIfNeededAsync(CancellationToken ct)
    {
        try
        {
            return await tasks.BeginTransactionAsync(ct);
        }
        catch (InvalidOperationException ex)
            when (ex.Message.Contains("already in a transaction", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }
    }

    /// <summary>
    /// Lưu thay đổi task, dịch lỗi thô của trigger PostgreSQL (khi race với
    /// kiểm tra ở trên) thành lỗi nghiệp vụ có mã để frontend hiện được.
    /// </summary>
    private async Task SaveTasksAsync(CancellationToken ct)
    {
        try
        {
            await tasks.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (MapTriggerException(ex) is not null)
        {
            throw MapTriggerException(ex)!;
        }
    }

    private static AppException? MapTriggerException(DbUpdateException ex)
    {
        var message = ex.InnerException?.Message ?? ex.Message;

        if (message.Contains("target exceeded", StringComparison.OrdinalIgnoreCase))
        {
            return new UnprocessableException(
                "task_target_exceeds_campaign",
                "Chỉ tiêu task vượt phần còn lại của chiến dịch. Lấy allocatedTaskQty ở danh sách chiến dịch để chia lại.");
        }

        if (message.Contains("must be within campaign", StringComparison.OrdinalIgnoreCase))
        {
            return new UnprocessableException(
                "task_deadline_outside_campaign",
                "Hạn của task nằm ngoài khoảng ngày của chiến dịch. Giới hạn ô chọn ngày theo startDate/endDate.");
        }

        if (message.Contains("no assigned task manager", StringComparison.OrdinalIgnoreCase)
            || message.Contains("must be the assigned task manager", StringComparison.OrdinalIgnoreCase))
        {
            return new UnprocessableException(
                "campaign_not_assigned_to_manager",
                "Chiến dịch chưa được giao cho bạn nên chưa nhận task. Nhờ Admin giao chiến dịch trước.");
        }

        return null;
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
