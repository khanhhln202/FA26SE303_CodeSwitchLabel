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
    /// <param name="doneOverride">
    /// Task Manager đếm việc đã ĐƯỢC DUYỆT xong; Speaker đếm việc đã THU xong. Cùng một task
    /// nhưng hai bộ số khác nhau, nên chỗ nào muốn bộ nào thì truyền bộ đó vào.
    /// </param>
    public static TaskProgressDto ProgressOf(
        this TaskRow r, DateTimeOffset now, int? doneOverride = null, int? submittedOverride = null)
    {
        var done = doneOverride ?? r.Done;

        return new(r.TaskType == TaskType.Recording ? "cặp câu" : "bản ghi",
            r.TargetQty,
            done,
            submittedOverride ?? r.Started,
            r.UsableItems,
            r.TotalItems,
            TaskStateMachine.Percent(done, r.TargetQty),
            r.Deadline.HasValue ? Math.Round((r.Deadline.Value - now).TotalHours, 1) : null,
            r.Deadline.HasValue && r.Deadline.Value < now && TaskStateMachine.AcceptsWork(r.Status));
    }

    // TEAM_003: Tính toán khả năng xoá của task (chỉ task rảnh: chưa gắn đợt + chưa có việc)
    public static (bool CanDelete, string? Reason) EvaluateCanDelete(this TaskRow r)
    {
        if (r.CampaignId.HasValue)
            return (false, $"Nhiệm vụ đang nằm trong đợt \"{r.CampaignName ?? ("#" + r.CampaignId)}\". Hãy gỡ khỏi đợt trước khi xoá.");

        if (r.Status == WorkTaskStatus.Completed)
            return (false, "Nhiệm vụ đã hoàn thành, không xoá được.");

        if (r.AssigneeCount > 0)
        {
            var names = string.Join(", ", r.Assignees.Select(a => a.FullName));
            var who = r.AssigneeCount == 1 ? names : $"{names} ({r.AssigneeCount} người)";
            return (false, $"Nhiệm vụ đã giao cho {who}. Dùng chức năng Huỷ nếu muốn dừng việc.");
        }

        if (r.TotalItems > 0 || r.Started > 0)
            return (false, "Nhiệm vụ đã có mục công việc hoặc dữ liệu nộp về. Dùng chức năng Huỷ để giữ dấu vết.");

        return (true, null);
    }

    public static TaskListItemDto ToListItem(this TaskRow r, DateTimeOffset now)
    {
        var (canDelete, reason) = r.EvaluateCanDelete();
        return new(r.TaskId,
            r.CampaignId,
            r.CampaignName,
            r.TaskType,
            r.Domain,
            r.Description,
            r.Status,
            [.. r.Assignees.Select(a => new TaskAssigneeDto(a.UserId, a.FullName))],
            r.Deadline,
            r.CreatedAt,
            r.ProgressOf(now),
            canDelete,
            reason);
    }

    // Speaker thấy task của chính mình nên không cần thời điểm tạo.
    // Tiến độ đếm cặp câu đã THU ĐỦ, không đợi duyệt: gửi xong hai bản là cộng 1.
    //
    // TEAM_010: task có thể giao cho nhiều người, nên trả CẢ HAI bộ số:
    //   MyDone   — việc của riêng người đang xem (để biết mình còn bao nhiêu)
    //   TeamDone — việc của cả nhóm (để biết cả đợt còn bao nhiêu)
    // Chỉ trả một bộ thì hoặc người đó tưởng mình làm cả nhiệm vụ, hoặc tưởng nhóm
    // mình không làm gì cả.
    public static SpeakerActiveTaskDto ToSpeakerActiveTask(
        this TaskRow r, DateTimeOffset now, int myDone, int mySubmitted) =>
        new(r.TaskId,
            r.Description,
            r.Description,
            r.CampaignName,
            r.TaskType,
            r.Status,
            r.Deadline,
            // Cả 'done' lẫn 'submitted' đều phải là của riêng người xem. Trước đây chỉ đè
            // 'done', nên task có ba người nhận thì cả ba cùng thấy tổng số bản ghi của nhóm
            // nằm trong myProgress — ai cũng tưởng mình nộp bằng đó.
            r.ProgressOf(now, myDone, mySubmitted),
            r.ProgressOf(now, r.SubmittedPairs),
            [.. r.Assignees.Select(a => new TaskAssigneeDto(a.UserId, a.FullName))]);
}

public class TaskService(
    ITaskRepository tasks,
    ICampaignRepository campaigns,
    ITaskProgressTracker tracker,
    ISystemConfigService config,
    TimeProvider clock) : ITaskService
{
    // TEAM_007: Múi giờ Việt Nam (UTC+7 / Asia/Ho_Chi_Minh) thống nhất với database
    private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);
    private static DateOnly ToVietnamDate(DateTimeOffset dt) => DateOnly.FromDateTime(dt.ToOffset(VietnamOffset).DateTime);

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

            // TEAM_013: bỏ luật "đợt đã đóng thì không nhận thêm task". Trước đây đợt `Completed`
            // nghĩa là đã chốt kết quả; giờ nó chỉ có nghĩa là mọi task đã xong, và gắn task mới
            // vào thì đợt tự quay lại `InProgress`. Chặn ở đây sẽ thành vòng luẩn quanh: đợt xong
            // → không gắn được → không bao giờ mở lại được nữa.

            // Hạn task phải nằm trong khoảng ngày của chiến dịch (trigger của database cũng chặn).
            var deadlineDate = ToVietnamDate(deadline);

            if (deadlineDate < campaign.StartDate || deadlineDate > campaign.EndDate)
            {
                throw new UnprocessableException(
                    "task_deadline_outside_campaign",
                    $"Hạn {deadlineDate:yyyy-MM-dd} phải nằm trong khoảng ngày của chiến dịch " +
                    $"{campaign.StartDate:yyyy-MM-dd} đến {campaign.EndDate:yyyy-MM-dd}.");
            }

            var domain = request.Domain ?? campaign.Domain;

            // TEAM_003: Nhiệm vụ phải cùng chủ đề với chiến dịch
            if (domain != campaign.Domain)
            {
                throw new UnprocessableException(
                    "task_domain_mismatch_campaign",
                    $"Chủ đề nhiệm vụ ({domain}) không khớp với chủ đề của chiến dịch #{campaignId} ({campaign.Domain}).");
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

        var taskDomain = request.Domain ?? (campaign != null ? campaign.Domain : ScriptDomain.ItTechnology);

        var task = new WorkTask
        {
            CampaignId = request.CampaignId,
            CreatedBy = createdById,
            TaskType = request.TaskType!.Value,
            Domain = taskDomain, // TEAM_007
            Description = Clean(request.Description),
            TargetQty = targetQty,
            Deadline = deadline,
            // TEAM_013: còn ba trạng thái (InProgress/Completed/Cancelled). Task vừa tạo chưa
            // có việc gì nhưng KHÔNG còn trạng thái nháp nữa — chờ người nhận thì vẫn là
            // InProgress, và chỉ tiêu vẫn được đặt trước rồi thêm mục vào sau.
            Status = WorkTaskStatus.InProgress,
            CreatedAt = now
        };

        tasks.Add(task);
        await SaveTasksAsync(ct);

        // TEAM_013: đợt vừa nhận nhiệm vụ đầu tiên thì phải rời Open.
        if (campaign is not null) await campaigns.RefreshStatusAsync(campaign.CampaignId, ct);

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

        // TEAM_013: đợt cũ cũng phải được tính lại — rút task ra khỏi đợt xong thì đợt có
        // thể hết nhiệm vụ (về lại Open) hoặc mọi task còn lại đều xong (về Completed).
        var previousCampaignId = task.CampaignId;

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

            // TEAM_013: bỏ luật chặn nhận thêm task vào đợt đã đóng — xem ở CreateAsync.

            // TEAM_003: Nhiệm vụ phải cùng chủ đề với chiến dịch
            if (task.Domain != campaign.Domain)
            {
                throw new UnprocessableException(
                    "task_domain_mismatch_campaign",
                    $"Chủ đề nhiệm vụ ({task.Domain}) không khớp với chủ đề của chiến dịch #{campaignId.Value} ({campaign.Domain}).");
            }

            if (task.Deadline.HasValue)
            {
                var deadlineDate = ToVietnamDate(task.Deadline.Value);
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

        if (campaignId.HasValue) await campaigns.RefreshStatusAsync(campaignId.Value, ct);
        if (previousCampaignId.HasValue && previousCampaignId != campaignId)
        {
            await campaigns.RefreshStatusAsync(previousCampaignId.Value, ct);
        }

        if (transaction is not null) await transaction.CommitAsync(ct);

        return await LoadDetailAsync(taskId, ct);
    }

    /// <summary>Gỡ task khỏi chiến dịch nhưng giữ lại task (C2). Không xoá assignments/items.</summary>
    /// <remarks>TEAM_017: cho phép detach cả task đã Cancelled (không qua LoadEditableAsync).</remarks>
    public async Task<TaskDetailDto> DetachAsync(
        long campaignId, long taskId, CancellationToken ct = default,
        long? callerId = null, bool isAdmin = false)
    {
        await using var transaction = await BeginTransactionIfNeededAsync(ct);
        var task = await tasks.GetForUpdateAsync(taskId, ct) ?? throw NotFound(taskId);

        if (task.CampaignId != campaignId)
        {
            throw new NotFoundException(
                "task_not_found", $"Task #{taskId} không thuộc chiến dịch #{campaignId}.");
        }

        // TEAM_017: detach không yêu cầu task ở trạng thái chỉnh sửa được;
        // chỉ cần người gọi có quyền trên chiến dịch cũ.
        await EnsureCampaignOwnerAsync(task, callerId, isAdmin, ct);

        var previousCampaignId = task.CampaignId;
        task.CampaignId = null;
        await SaveTasksAsync(ct);

        if (previousCampaignId.HasValue) await campaigns.RefreshStatusAsync(previousCampaignId.Value, ct);

        if (transaction is not null) await transaction.CommitAsync(ct);
        return await LoadDetailAsync(taskId, ct);
    }

    public async Task<PagedResult<TaskListItemDto>> SearchAsync(
        TaskSearchRequest request, CancellationToken ct = default)
    {
        var now = clock.GetUtcNow();

        // TEAM_003: Truyền request.Domain xuống repository
        var (rows, total) = await tasks.SearchAsync(
            request.TaskType, request.Status, request.AssigneeId, request.Domain, request.Overdue,
            now, request.Page, request.PageSize, request.ToSort<TaskSortField>(), ct);

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

        // TEAM_003: Cập nhật chủ đề (nếu task đang gắn chiến dịch thì phải khớp chủ đề chiến dịch)
        if (request.Domain.HasValue)
        {
            var newDomain = request.Domain.Value;
            if (task.CampaignId.HasValue)
            {
                var campaign = await campaigns.GetAsync(task.CampaignId.Value, ct);
                if (campaign is not null && campaign.Domain != newDomain)
                {
                    throw new UnprocessableException(
                        "task_domain_mismatch_campaign",
                        $"Chủ đề nhiệm vụ ({newDomain}) không khớp với chủ đề của chiến dịch #{task.CampaignId.Value} ({campaign.Domain}).");
                }
            }
            task.Domain = newDomain;
        }

        if (request.Deadline.HasValue)
        {
            var deadline = ToUtcFuture(request.Deadline.Value, clock.GetUtcNow());
            var campaign = task.CampaignId.HasValue
                ? await campaigns.GetAsync(task.CampaignId.Value, ct)
                : null;
            if (campaign is not null)
            {
                var deadlineDate = ToVietnamDate(deadline);
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

            // TEAM_013: trước đây nhánh này bỏ qua khi task còn ở trạng thái `Draft`
            // (chưa giao cho ai). `Draft` đã bị bỏ, nên điều kiện được hỏi lại đúng bằng nghĩa
            // cũ: CHƯA CÓ NGƯỜI NHẬN thì cứ đặt chỉ tiêu trước, thêm mục vào sau.
            var active = await tasks.GetActiveAssignmentsAsync(taskId, ct);
            if (active.Count > 0)
            {
                var share = PerPersonQuota(request.TargetQty.Value, active.Count);
                var (workable, blocked) = await CountWorkableAsync(task, active, null, ct);

                // TEAM_018: task duyệt trong đợt được nâng chỉ tiêu trước khi có bản ghi.
                if (RequiresItemsToAssign(task) && share > workable)
                {
                    throw new UnprocessableException(
                        "target_exceeds_items",
                        DescribeShortfall(share, workable, blocked) + " Thêm mục hoặc hạ chỉ tiêu.");
                }
            }

            task.TargetQty = request.TargetQty.Value;
        }

        await SaveTasksAsync(ct);

        // TEAM_013: Task Manager bấm tay đổi trạng thái. Giá trị này được giữ nguyên —
        // cố ý KHÔNG gọi tracker ở đây, vì tracker sẽ tính lại từ số liệu và ghi đè mất
        // lựa chọn của người ngay lập tức. Trạng thái bấm tay chỉ bị tính lại khi có việc
        // thật xảy ra sau đó (nộp bản ghi, duyệt, đổi chỉ tiêu...).
        if (request.Status.HasValue) await ApplyManualStatusAsync(task, request.Status.Value, ct);

        // Chỉ đổi CHỈ TIÊU mới là việc thật có ảnh hưởng tới trạng thái: nâng thì task
        // đang xong phải mở lại, hạ thì task có thể xong ngay.
        //
        // Trước TEAM_013, tracker chạy ở MỌI lần PATCH — nên chỉ sửa mô tả cũng làm trạng
        // thái Task Manager vừa bấm tay biến mất. Sửa tên, đổi hạn, đổi chủ đề đều không
        // đụng tới số liệu nên không có lý do gì để tính lại.
        if (!request.Status.HasValue && request.TargetQty.HasValue)
        {
            await tracker.RefreshStatusAsync(taskId, ct);
        }

        if (task.CampaignId.HasValue) await campaigns.RefreshStatusAsync(task.CampaignId.Value, ct);

        if (transaction is not null) await transaction.CommitAsync(ct);

        return await LoadDetailAsync(taskId, ct);
    }

    /// <summary>
    /// TEAM_013: dùng chung cho nút Huỷ và cho việc Task Manager chọn `Cancelled` tay,
    /// để hai đường không lệch nhau. Các mục giữ nguyên để còn dấu vết công việc; lượt giao
    /// đang hoạt động thì chuyển sang `cancelled` — task đã dừng thì giữ chỗ là vô nghĩa.
    /// </summary>
    private async Task ApplyManualStatusAsync(WorkTask task, WorkTaskStatus status, CancellationToken ct)
    {
        if (task.Status == status) return;

        task.Status = status;

        if (status == WorkTaskStatus.Cancelled)
        {
            foreach (var assignment in await tasks.GetActiveAssignmentsAsync(task.TaskId, ct))
            {
                assignment.AssignmentStatus = AssignmentStatus.Cancelled;
            }
        }

        await SaveTasksAsync(ct);
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

        // TEAM_010: nhóm người nhận, không phải một người. Mọi luật "câu này thuộc về
        // người nhận task" giờ áp cho bất kỳ ai trong nhóm.
        var assignees = await tasks.GetActiveAssignmentsAsync(taskId, ct);
        var assigneeIds = assignees.Select(a => a.UserId).ToArray();

        var skipped = new List<SkippedItemDto>();
        var now = clock.GetUtcNow();
        var borrowedFrom = new List<long>();
        List<string> accepted;

        if (task.TaskType == TaskType.Recording)
        {
            string[] ids = hasIds
                ? [.. request.Ids.Distinct()]
                : [.. await tasks.FindScriptCandidatesAsync(
                    taskId, assigneeIds, request.AutoFill!.Domain ?? task.Domain, request.AutoFill.Count, ct)];

            accepted = await FilterScriptsAsync(taskId, task.Domain, ids, assigneeIds, skipped, ct, borrowedFrom);

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
                    taskId, assigneeIds, request.AutoFill!.SpeakerId, request.AutoFill.Count, roundsRequired, ct)];

            accepted = await FilterRecordingsAsync(taskId, ids, assigneeIds, roundsRequired, skipped, ct);

            tasks.AddRecordingItems(accepted.Select(id => new TaskRecording
            {
                TaskId = taskId,
                RecordingId = id,
                Status = TaskRecordingStatus.Queued,
                IncludedAt = now
            }));
        }

        await SaveTasksAsync(ct);

        // Câu mượn từ task khác làm chỉ tiêu task cũ vượt quá số mục còn lại — hạ xuống cho khớp,
        // rồi tính lại trạng thái. Không hạ thì task cũ treo vĩnh viễn ở chỗ không đủ việc.
        foreach (var oldTaskId in borrowedFrom.Distinct().Where(id => id != taskId))
        {
            await ShrinkTargetToItemsAsync(oldTaskId, ct);
            await tracker.RefreshStatusAsync(oldTaskId, ct);
        }

        await tracker.RefreshStatusAsync(taskId, ct);
        if (transaction is not null) await transaction.CommitAsync(ct);

        return new AddTaskItemsResult(accepted.Count, skipped, await LoadDetailAsync(taskId, ct));
    }

    /// <summary>Hạ chỉ tiêu của task cho vừa đủ số mục còn lại, không hạ thấp hơn mức đang làm được.</summary>
    private async Task ShrinkTargetToItemsAsync(long taskId, CancellationToken ct)
    {
        var task = await tasks.GetForUpdateAsync(taskId, ct);
        if (task is null) return;

        // TEAM_013: cũng bỏ qua khi task CHƯA CÓ NGƯỜI NHẬN — cùng nghĩa với `Draft`
        // cũ mà nhánh này dùng để bỏ qua, chỉ là hỏi bằng tín hiệu còn tồn tại thay vì
        // hỏi bằng trạng thái (trạng thái `Draft` không còn nữa).
        var active = await tasks.GetActiveAssignmentsAsync(taskId, ct);
        if (active.Count == 0) return;

        var (workable, _) = await CountWorkableAsync(task, active, null, ct);

        if (workable < task.TargetQty)
        {
            task.TargetQty = workable;
            await SaveTasksAsync(ct);
        }
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

    /// <summary>
    /// TEAM_010: giao task cho NHIỀU người cùng lúc. Trước đây giao là ĐUỔI người cũ
    /// (chuyển lượt giao của họ sang Reassigned) — đúng khi mỗi task chỉ một người,
    /// nhưng sai ngay khi cho phép nhiều người. Giờ giao thêm, không đuổi ai.
    /// </summary>
    public async Task<TaskDetailDto> AssignAsync(
        long taskId, long[] userIds, CancellationToken ct = default,
        long? callerId = null, bool isAdmin = false)
    {
        var wanted = userIds.Distinct().ToArray();

        if (wanted.Length == 0)
        {
            throw new UnprocessableException("assignee_required", "Phải chọn ít nhất một người nhận.");
        }

        var task = await LoadMutableAsync(taskId, ct);
        await EnsureCampaignOwnerAsync(task, callerId, isAdmin, ct);

        var current = await tasks.GetActiveAssignmentsAsync(taskId, ct);
        var already = current.Select(a => a.UserId).ToHashSet();

        var toAdd = wanted.Where(id => !already.Contains(id)).ToArray();

        if (toAdd.Length == 0)
        {
            // TEAM_017: không có người mới để thêm → coi như thành công (no-op),
            // tránh ném ConflictException khi frontend gửi lại danh sách không đổi.
            await tracker.RefreshStatusAsync(taskId, ct);
            return await LoadDetailAsync(taskId, ct);
        }

        var requiredRole = task.TaskType == TaskType.Recording ? RoleName.Speaker : RoleName.Reviewer;
        var fresh = new List<AppUser>(toAdd.Length);

        foreach (var userId in toAdd)
        {
            var user = await tasks.GetUserWithRoleAsync(userId, ct)
                       ?? throw new NotFoundException("user_not_found", $"Không tìm thấy tài khoản #{userId}.");

            if (user.Status != UserStatus.Active)
            {
                throw new UnprocessableException("user_inactive", $"Tài khoản {user.Email} đang bị vô hiệu hoá.");
            }

            if (user.Role.RoleName != requiredRole)
            {
                throw new UnprocessableException(
                    "assignee_role_mismatch",
                    $"Task {task.TaskType} chỉ giao được cho {requiredRole}, còn {user.Email} là {user.Role.RoleName}.");
            }

            fresh.Add(user);
        }

        // TEAM_010: chỉ tiêu là của CẢ NHÓM. Mỗi người khoảng target/n, nên không
        // đòi mỗi người phải làm đủ cả target — làm vậy thì giao người thứ hai vào
        // là luôn trượt điều kiện dù tổng số mục vẫn dư.
        var share = PerPersonQuota(task.TargetQty, already.Count + toAdd.Length);
        var afterAssigning = new List<TaskAssignment>(current);
        afterAssigning.AddRange(fresh.Select(f => new TaskAssignment { TaskId = taskId, UserId = f.UserId }));
        var (workable, blocked) = await CountWorkableAsync(task, afterAssigning, null, ct);

        // TEAM_018: task duyệt trong đợt nhận việc qua đường tự đẩy lúc Speaker nộp, nên hàng
        // chờ rỗng lúc giao KHÔNG phải lỗi — giao reviewer trước, bản ghi tự tới sau là luồng
        // chính. Task thu âm và task duyệt ngoài đợt vẫn phải đủ mục trước khi giao.
        if (RequiresItemsToAssign(task) && share > workable)
        {
            throw new UnprocessableException(
                "target_exceeds_items",
                DescribeShortfall(share, workable, blocked) +
                $" Mỗi người cần khoảng {share}/{task.TargetQty} mục (chia cho {already.Count + toAdd.Length} người nhận). " +
                "Thêm mục, gỡ các mục đó, hoặc hạ chỉ tiêu.");
        }

        await using var transaction = await BeginTransactionIfNeededAsync(ct);

        var now = clock.GetUtcNow();

        foreach (var user in fresh)
        {
            tasks.AddAssignment(new TaskAssignment
            {
                TaskId = taskId,
                UserId = user.UserId,
                AssignedAt = now,
                AssignmentStatus = AssignmentStatus.Active
            });
        }

        await SaveTasksAsync(ct);

        // TEAM_013: giao task không còn đổi trạng thái (task đã là InProgress từ lúc tạo),
        // nhưng tracker vẫn chạy để một task bị Task Manager bấm tay về Completed rồi
        // được giao thêm người sẽ tính lại đúng theo số liệu.
        await tracker.RefreshStatusAsync(taskId, ct);
        if (transaction is not null) await transaction.CommitAsync(ct);

        return await LoadDetailAsync(taskId, ct);
    }

    /// <summary>
    /// TEAM_010: gỡ một người khỏi task, giữ lịch sử (lượt giao chuyển sang Reassigned).
    ///
    /// Không cho gỡ người CUỐI CÙNG khi task đã có việc: task sẽ không còn ai làm tiếp,
    /// dữ liệu đã làm thì còn nguyên nhưng không ai dùng được nữa. Muốn dừng hẳn thì dùng Huỷ.
    ///
    /// TEAM_013: trước đây lý do cụ thể hơn là "task rơi về Draft, mà Draft không nhận việc
    /// nữa" — trạng thái `Draft` đã bị bỏ nên luật này giữ nguyên vì lý do thật của nó.
    /// </summary>
    public async Task<TaskDetailDto> UnassignAsync(
        long taskId, long userId, CancellationToken ct = default,
        long? callerId = null, bool isAdmin = false)
    {
        await using var transaction = await BeginTransactionIfNeededAsync(ct);
        var task = await LoadEditableAsync(taskId, ct);
        await EnsureCampaignOwnerAsync(task, callerId, isAdmin, ct);

        var active = await tasks.GetActiveAssignmentsAsync(taskId, ct);
        var mine = active.FirstOrDefault(a => a.UserId == userId);

        if (mine is null)
        {
            throw new NotFoundException(
                "assignee_not_found", $"Người #{userId} không nhận task #{taskId}.");
        }

        if (active.Count == 1)
        {
            var row = await tasks.GetRowAsync(taskId, ct);

            if (row is not null && (row.Started > 0 || row.TotalItems > 0))
            {
                throw new ConflictException(
                    "cannot_remove_last_assignee",
                    $"Đây là người nhận cuối cùng của task #{taskId} mà task đã có việc. " +
                    "Gỡ người lúc này thì không ai làm tiếp được, dữ liệu đã làm thì còn nguyên " +
                    "nhưng không ai dùng. Dùng chức năng Huỷ nếu muốn dừng hẳn.");
            }
        }

        mine.AssignmentStatus = AssignmentStatus.Reassigned;
        await SaveTasksAsync(ct);

        await tracker.RefreshStatusAsync(taskId, ct);
        if (transaction is not null) await transaction.CommitAsync(ct);

        return await LoadDetailAsync(taskId, ct);
    }

    /// <summary>Chỉ tiêu mỗi người phải đạt khi task có n người nhận. Luôn ít nhất 1.</summary>
    private static int PerPersonQuota(int targetQty, int assigneeCount) =>
        assigneeCount <= 1 ? targetQty : Math.Max(1, (int)Math.Ceiling(targetQty / (double)assigneeCount));

    /// <summary>
    /// TEAM_018: task duyệt trong đợt nhận việc qua đường tự đẩy lúc Speaker nộp, nên hàng chờ
    /// rỗng lúc giao KHÔNG phải lỗi — giao reviewer trước, bản ghi tự tới sau là luồng chính.
    /// Task thu âm và task duyệt ngoài đợt vẫn phải đủ mục trước khi giao/nâng chỉ tiêu.
    /// </summary>
    private static bool RequiresItemsToAssign(WorkTask task) =>
        task.TaskType != TaskType.Review || task.CampaignId is null;

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

        // TEAM_013: đi qua helper chung với đường "Task Manager chọn Cancelled tay" ở
        // UpdateAsync — một nơi ghi trạng thái, một nơi huỷ lượt giao.
        await ApplyManualStatusAsync(task, WorkTaskStatus.Cancelled, ct);

        // Các mục giữ nguyên để còn dấu vết. Task đã huỷ không giữ chỗ gì nữa:
        // truy vấn tự lấp chỉ tránh những mục đang nằm trong task còn chạy.
        if (task.CampaignId.HasValue) await campaigns.RefreshStatusAsync(task.CampaignId.Value, ct);

        return await LoadDetailAsync(taskId, ct);
    }

    // TEAM_003: Xoá cứng task rảnh (chưa gắn đợt nào và chưa có việc/mục/lượt giao)
    public async Task DeleteAsync(
        long taskId, CancellationToken ct = default,
        long? callerId = null, bool isAdmin = false)
    {
        await using var transaction = await BeginTransactionIfNeededAsync(ct);
        var task = await tasks.GetForUpdateAsync(taskId, ct) ?? throw NotFound(taskId);
        await EnsureCampaignOwnerAsync(task, callerId, isAdmin, ct);

        // 1. Chặn xoá khi task vẫn đang nằm trong chiến dịch
        if (task.CampaignId.HasValue)
        {
            var cName = (await campaigns.GetAsync(task.CampaignId.Value, ct))?.CampaignName;
            throw new ConflictException(
                "task_in_campaign",
                $"Nhiệm vụ đang nằm trong đợt \"{cName ?? ("#" + task.CampaignId)}\". Hãy gỡ khỏi đợt trước khi xoá.");
        }

        // 2. Chặn xoá khi task đã hoàn thành
        if (task.Status == WorkTaskStatus.Completed)
        {
            throw new ConflictException("task_completed", $"Nhiệm vụ #{taskId} đã hoàn thành, không xoá được.");
        }

        // 3. Chặn xoá khi task đã có việc (lượt giao, mục script/bản ghi, bản ghi nộp về)
        var hasWork = await tasks.HasWorkAsync(taskId, ct);
        if (hasWork)
        {
            throw new ConflictException(
                "task_has_work",
                $"Nhiệm vụ #{taskId} đã có người nhận, mục công việc hoặc dữ liệu nộp về. Dùng chức năng Huỷ nếu không làm nữa để giữ dấu vết.");
        }

        tasks.RemoveTask(task);
        await SaveTasksAsync(ct);
        if (transaction is not null) await transaction.CommitAsync(ct);
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

        var active = new List<SpeakerActiveTaskDto>(rows.Count);

        foreach (var row in rows)
        {
            // TEAM_010: đếm riêng phần việc của người này. Task có 3 người nhận thì
            // cả ba sẽ thấy cùng TaskRow.Done nếu không tra riêng.
            var mine = await tasks.GetAssigneeDoneAsync(row.TaskId, row.TaskType, ct);
            var nop = await tasks.GetAssigneeSubmittedAsync(row.TaskId, row.TaskType, ct);

            active.Add(row.ToSpeakerActiveTask(
                now, mine.GetValueOrDefault(speakerId), nop.GetValueOrDefault(speakerId)));
        }

        var (submitted, approved) = await tasks.GetSpeakerTotalsAsync(speakerId, ct);

        return new SpeakerProgressDto(submitted, approved, active);
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
                p.CampaignId, p.CampaignName, p.Domain, p.CampaignTargetQty, p.StartDate, p.EndDate,
                p.CampaignStatus, p.AssignedTo, p.AllocatedTaskQty, p.RemainingTaskQty,
                p.TaskCount, p.CompletedTaskCount))
            .ToList();

        var byAssignee = await GetAssigneeSummaryAsync(ct);

        // Đếm task còn chạy theo loại + quá hạn: chỉ cần Total nên pageSize = 1.
        // Chỉ đếm nên thứ tự không quan trọng — SortSpec rỗng là thứ tự mặc định.
        // TEAM_013: chỉ còn một trạng thái "đang chạy" là InProgress, nên không cộng
        // `Open` với `InProgress` nữa.
        var (_, activeRec) = await tasks.SearchAsync(
            TaskType.Recording, WorkTaskStatus.InProgress, null, null, null, now, 1, 1, default, ct);
        var (_, activeRev) = await tasks.SearchAsync(
            TaskType.Review, WorkTaskStatus.InProgress, null, null, null, now, 1, 1, default, ct);
        var (_, overdue) = await tasks.SearchAsync(
            null, null, null, null, true, now, 1, 1, default, ct);

        return new TaskManagerOverviewDto(mine, byAssignee, activeRec, activeRev, overdue);
    }

    // ----------------------------------------------------------------- nội bộ

    /// <returns>Danh sách id được nhận, và các task cũ cần cập nhật lại sau khi mượn câu.</returns>
    private async Task<List<string>> FilterScriptsAsync(
        long taskId, ScriptDomain taskDomain, string[] ids, long[] assigneeIds, List<SkippedItemDto> skipped,
        CancellationToken ct, List<long>? borrowedFromTasks = null)
    {
        var existing = (await tasks.GetScriptItemsAsync(taskId, ct)).Select(i => i.ScriptId).ToHashSet();
        var found = (await tasks.GetScriptsAsync(ids, ct)).ToDictionary(s => s.ScriptId);
        var queuedElsewhere = await tasks.FindScriptsQueuedInOtherActiveTasksAsync(ids, taskId, assigneeIds, ct);
        var accepted = new List<string>();

        // Chỉ mượn được câu của chính những người nhận task này. Task chưa giao cho ai thì
        // không gắn với ai, nên không được cướp câu đang nằm trong task của người khác.
        var borrowable = queuedElsewhere
            .Where(id => !found.TryGetValue(id, out var s)
                || !s.Recordings.Any(r => r.Status != RecordingStatus.QcFailed))
            .ToHashSet();

        if (assigneeIds.Length == 0) borrowable.Clear();

        foreach (var id in ids)
        {
            if (existing.Contains(id))
                skipped.Add(new SkippedItemDto(id, "Đã có trong task."));
            else if (!found.TryGetValue(id, out var script))
                skipped.Add(new SkippedItemDto(id, "Không tồn tại."));
            else if (script.Status != ScriptStatus.Validated)
                skipped.Add(new SkippedItemDto(id, $"Đang ở trạng thái {script.Status}, chỉ nhận câu đã duyệt nội dung."));
            else if (script.Domain != taskDomain)
                skipped.Add(new SkippedItemDto(id, $"Chủ đề câu ({script.Domain}) không khớp với chủ đề nhiệm vụ ({taskDomain})."));
            // TEAM_010: một cặp câu chỉ một người đọc. Với nhóm n người nhận, câu do
            // một người NGOÀI nhóm đã thu thì cả nhóm đều không thu được nữa.
            else if (assigneeIds.Length > 0 &&
                     script.Recordings.Any(r => !assigneeIds.Contains(r.SpeakerId) && r.Status != RecordingStatus.QcFailed))
                skipped.Add(new SkippedItemDto(id, "Cặp câu này do người đọc khác thu."));
            else if (queuedElsewhere.Contains(id) && borrowable.Contains(id))
            {
                // Câu còn nằm trong task khác nhưng CHƯA có bản ghi nào: chuyển sang task này cho
                // mượn, thay vì báo lỗi. Người nhận chỉ nhận nhiệm vụ, chưa bắt tay vào làm.
                accepted.Add(id);
            }
            else if (queuedElsewhere.Contains(id))
                skipped.Add(new SkippedItemDto(id, assigneeIds.Length > 0
                    ? "Cặp câu đang nằm trong một task khác đang chạy và đã có bản ghi nộp."
                    : "Cặp câu đang nằm trong một task khác đang chạy — giao task cho người đọc trước rồi thử lại."));
            else
                accepted.Add(id);
        }

        // Chỉ mượn câu nào THỰC SỰ được nhận vào task này. Câu bị bỏ vì lý do khác (đã có trong
        // task, không tồn tại, chưa duyệt nội dung…) phải nằm yên ở task cũ — gỡ đi là mất luôn.
        borrowable.IntersectWith(accepted);

        if (borrowable.Count > 0)
        {
            var stale = await tasks.GetQueuedScriptItemsInOtherActiveTasksAsync(
                [.. borrowable], taskId, assigneeIds, ct);

            foreach (var item in stale)
            {
                // Trùng nhau là bình thường khi một câu nằm ở nhiều task: bỏ hết các bản cũ.
                if (found.TryGetValue(item.ScriptId, out var s)
                    && s.Recordings.Any(r => r.Status != RecordingStatus.QcFailed))
                {
                    continue;
                }

                borrowedFromTasks?.Add(item.TaskId);
                tasks.Remove(item);
            }
        }

        return accepted;
    }

    private async Task<List<string>> FilterRecordingsAsync(
        long taskId, string[] ids, long[] assigneeIds, int roundsRequired,
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
            // TEAM_010: bản ghi của bất kỳ ai trong nhóm nhận task đều không thể đưa
            // vào task này — người duyệt không được duyệt bản ghi của chính mình, và
            // 3 lượt duyệt phải là 3 người khác nhau.
            else if (assigneeIds.Length > 0 && assigneeIds.Contains(recording.SpeakerId))
                skipped.Add(new SkippedItemDto(id, "Người nhận task chính là người thu bản ghi này."));
            else if (assigneeIds.Length > 0 &&
                     recording.Reviews.Any(v => assigneeIds.Contains(v.ReviewerId)))
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
        // TEAM_013: cùng nghĩa với nhánh `Draft` cũ — chưa giao cho ai thì chưa có mục nào
        // để kiểm tra, cứ để Task Manager sửa tiếp.
        var active = await tasks.GetActiveAssignmentsAsync(task.TaskId, ct);
        if (active.Count == 0) return;

        var (workable, blocked) = await CountWorkableAsync(task, active, itemId, ct);

        // TEAM_018: task duyệt trong đợt tự đầy lại khi có bản ghi mới, nên gỡ một mục
        // không làm task kẹt — cùng lý do với RequiresItemsToAssign ở đường giao việc.
        if (RequiresItemsToAssign(task) && task.TargetQty > workable)
        {
            throw new ConflictException(
                "target_exceeds_items",
                $"Gỡ mục {itemId} thì task không còn đủ mục. " +
                DescribeShortfall(task.TargetQty, workable, blocked) + " Hạ chỉ tiêu trước.");
        }
    }

    /// <summary>
    /// Số mục nhóm người nhận còn làm được: bỏ mục đã loại, và bỏ mục mà KHÔNG AI
    /// trong nhóm làm được — cặp câu của người đọc khác, hoặc bản ghi do chính người
    /// nhận task thu (người duyệt không được duyệt bản của mình).
    ///
    /// TEAM_010: trước đây đếm theo MỘT người nhận. Nhiều người thì phải hỏi "cả nhóm
    /// còn làm được bao nhiêu", không phải "người này còn làm được bao nhiêu" — nếu
    /// tính theo một người thì có thể báo thiếu mục trong khi người khác vẫn làm được.
    ///
    /// <paramref name="Blocked"/> trả về mọi mục bị ít nhất một người chặn, để thông báo
    /// lỗi nói rõ vấn đề nằm ở đâu; số trừ vào chỉ tiêu là <c>BlockedForEveryone</c>.
    /// </summary>
    private async Task<(int Workable, List<string> Blocked)> CountWorkableAsync(
        WorkTask task, List<TaskAssignment> active, string? excludingItemId, CancellationToken ct)
    {
        var row = await tasks.GetRowAsync(task.TaskId, ct) ?? throw NotFound(task.TaskId);

        var usable = row.UsableItems - (excludingItemId is not null ? 1 : 0);

        if (active.Count == 0)
        {
            // Chưa giao cho ai: mọi mục còn dùng được thì ai nhận vào cũng làm được.
            return (Math.Max(usable, 0), []);
        }

        var perUser = new List<HashSet<string>>(active.Count);

        foreach (var assignment in active)
        {
            var own = await tasks.GetBlockedItemIdsAsync(task.TaskId, assignment.UserId, task.TaskType, ct);

            perUser.Add([.. own.Where(id => id != excludingItemId)]);
        }

        var blocked = perUser.SelectMany(s => s).Distinct().ToList();

        // Giao các danh sách chặn: mục nào xuất hiện ở MỌI người thì mới thật sự
        // không ai làm được. Chỉ một người chặn thì người khác vẫn nhận phần việc đó.
        var blockedForEveryone = perUser[0].Count(id => perUser.All(set => set.Contains(id)));

        return (Math.Max(usable - blockedForEveryone, 0), blocked);
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
        config.GetIntAsync(ConfigKeys.ReviewRoundsRequired, ReviewRules.DefaultRoundsRequired, ct);

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
