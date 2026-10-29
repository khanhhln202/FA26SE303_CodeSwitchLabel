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
        this TaskRow r, DateTimeOffset now, int? doneOverride = null,
        int? submittedOverride = null, int? targetOverride = null)
    {
        var done = doneOverride ?? r.Done;
        var target = targetOverride ?? r.TargetQty;

        return new(r.TaskType == TaskType.Recording ? "cặp câu" : "bản ghi",
            target,
            done,
            submittedOverride ?? r.Started,
            r.UsableItems,
            r.TotalItems,
            TaskStateMachine.Percent(done, target),
            r.Deadline.HasValue ? Math.Round((r.Deadline.Value - now).TotalHours, 1) : null,
            r.Deadline.HasValue && r.Deadline.Value < now && TaskStateMachine.AcceptsWork(r.Status));
    }

    // Tính toán khả năng xoá của task (đã gỡ khỏi đợt + chưa có dữ liệu nộp thật)
    public static (bool CanDelete, string? Reason) EvaluateCanDelete(this TaskRow r)
    {
        if (r.CampaignId.HasValue)
            return (false, $"Nhiệm vụ đang nằm trong đợt \"{r.CampaignName ?? ("#" + r.CampaignId)}\". Hãy gỡ khỏi đợt trước khi xoá.");

        if (r.Status == WorkTaskStatus.Completed)
            return (false, "Nhiệm vụ đã hoàn thành, không xoá được.");

        if (r.Started > 0)
            return (false, "Nhiệm vụ đã có dữ liệu nộp/duyệt. Dùng chức năng Huỷ để giữ dấu vết.");

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
            [.. r.Assignees.Select(a => new TaskAssigneeDto(a.UserId, a.FullName, a.TargetQty))],
            r.Deadline,
            r.CreatedAt,
            r.ProgressOf(now),
            canDelete,
            reason);
    }

    // Speaker thấy task của chính mình nên không cần thời điểm tạo.
    // Tiến độ đếm cặp câu đã THU ĐỦ, không đợi duyệt: gửi xong hai bản là cộng 1.
    //
    // task có thể giao cho nhiều người, nên trả CẢ HAI bộ số:
    //   MyDone   — việc của riêng người đang xem (để biết mình còn bao nhiêu)
    //   TeamDone — việc của cả nhóm (để biết cả đợt còn bao nhiêu)
    // Chỉ trả một bộ thì hoặc người đó tưởng mình làm cả nhiệm vụ, hoặc tưởng nhóm
    // mình không làm gì cả.
    public static SpeakerActiveTaskDto ToSpeakerActiveTask(
        this TaskRow r, DateTimeOffset now, int myDone, int mySubmitted, int myTargetQty) =>
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
            //
            // V12: chỉ tiêu của riêng người nhận nằm ở task_assignment.target_qty, không
            // còn lấy chỉ tiêu của cả nhóm cho myProgress.
            r.ProgressOf(now, myDone, mySubmitted, myTargetQty),
            r.ProgressOf(now, r.SubmittedPairs),
            [.. r.Assignees.Select(a => new TaskAssigneeDto(a.UserId, a.FullName, a.TargetQty))]);
}

public class TaskService(
    ITaskRepository tasks,
    ICampaignRepository campaigns,
    IScriptQueueRepository queue,
    ITaskProgressTracker tracker,
    ISystemConfigService config,
    TimeProvider clock) : ITaskService
{
    // Múi giờ Việt Nam (UTC+7 / Asia/Ho_Chi_Minh) thống nhất với database
    private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);
    private static DateOnly ToVietnamDate(DateTimeOffset dt) => DateOnly.FromDateTime(dt.ToOffset(VietnamOffset).DateTime);

    public async Task<TaskDetailDto> CreateAsync(
        CreateTaskRequest request, long createdById, CancellationToken ct = default)
    {
        // Keep task, assignments, and selected recording pairs atomic. The repository
        // locks candidate script rows until commit so another create cannot pick them.
        await using var transaction = await BeginTransactionIfNeededAsync(ct);
        var now = clock.GetUtcNow();

        var targetQty = request.TargetQty!.Value;
        var deadline = ToUtcFuture(request.Deadline!.Value, now);

        // phân công thành viên ngay lúc tạo (tùy chọn)
        List<long> assigneeUserIds = request.AssigneeUserIds?.Distinct().ToList() ?? [];
        int[]? assignmentQuotas = null;

        if (assigneeUserIds.Count > 0)
        {
            // Chỉ tiêu phải >= số người nhận
            if (targetQty < assigneeUserIds.Count)
            {
                throw new UnprocessableException(
                    "target_less_than_assignees",
                    $"Chỉ tiêu {targetQty} nhỏ hơn số người nhận {assigneeUserIds.Count}. Mỗi người nhận cần ít nhất 1 mục.");
            }

            // Chia quota: phần dư về người được chọn trước (cùng công thức SplitQuota)
            assignmentQuotas = SplitQuota(targetQty, assigneeUserIds.Count);

            // Xác thực từng thành viên
            foreach (var userId in assigneeUserIds)
            {
                await EnsureEligibleAssigneeAsync(userId, request.TaskType!.Value, ct);
            }
        }

        // campaignId null = task độc lập (C1): bỏ qua mọi kiểm tra chiến dịch.
        // Trigger trg_task_campaign_window / trg_task_campaign_target / trg_task_creator_assigned
        // đều RETURN NEW ngay khi NEW.campaign_id IS NULL.
        Campaign? campaign = null;
        if (request.CampaignId.HasValue)
        {
            var campaignId = request.CampaignId.Value;

            campaign = await campaigns.GetAsync(campaignId, ct)
                ?? throw new NotFoundException("campaign_not_found", $"Không tìm thấy chiến dịch #{campaignId}.");

            // ERD — Task Manager chỉ tạo được task trong chiến dịch Admin đã giao cho chính
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

            // bỏ luật "đợt đã đóng thì không nhận thêm task". Trước đây đợt `Completed`
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

            // Nhiệm vụ phải cùng chủ đề với chiến dịch
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

        // Task thu âm: lấy danh sách câu RIÊNG cho từng Speaker. Hai Speaker được nhận
        // cùng một câu, nên kho câu ít hơn chỉ tiêu vẫn tạo task — mỗi người chỉ nhận
        // tối đa số câu mình còn thu được.
        List<List<string>>? recordingScriptsByUser = null;
        if (assigneeUserIds.Count > 0 && request.TaskType == TaskType.Recording)
        {
            recordingScriptsByUser = [];
            foreach (var userId in assigneeUserIds)
            {
                recordingScriptsByUser.Add(await tasks.GetRecordableScriptIdsByDomainAsync(
                    taskDomain, userId, targetQty, ct));
            }
        }

        var task = new WorkTask
        {
            CampaignId = request.CampaignId,
            CreatedBy = createdById,
            TaskType = request.TaskType!.Value,
            Domain = taskDomain, // TEAM_007
            Description = Clean(request.Description),
            TargetQty = targetQty,
            Deadline = deadline,
            // còn ba trạng thái (InProgress/Completed/Cancelled). Task vừa tạo chưa
            // có việc gì nhưng KHÔNG còn trạng thái nháp nữa — chờ người nhận thì vẫn là
            // InProgress, và chỉ tiêu vẫn được đặt trước rồi thêm mục vào sau.
            Status = WorkTaskStatus.InProgress,
            CreatedAt = now
        };

        tasks.Add(task);
        await SaveTasksAsync(ct);

        // tạo phân công cùng lúc với task
        if (assigneeUserIds.Count > 0)
        {
            var scriptItems = new List<TaskScript>();

            for (var i = 0; i < assigneeUserIds.Count; i++)
            {
                var userId = assigneeUserIds[i];

                // Cộng ticks để thứ tự chọn được giữ nguyên khi đọc lại (ORDER BY assigned_at).
                tasks.AddAssignment(new TaskAssignment
                {
                    TaskId = task.TaskId,
                    UserId = userId,
                    AssignedAt = now.AddTicks(i),
                    AssignmentStatus = AssignmentStatus.Active,
                    TargetQty = assignmentQuotas![i]
                });

                // Recording: mỗi người nhận các câu CHÍNH HỌ chưa thu, tối đa quota.
                // Danh sách của hai Speaker có thể trùng nhau — đó là chủ ý.
                if (recordingScriptsByUser is not null)
                {
                    var scripts = recordingScriptsByUser[i];
                    for (var j = 0; j < assignmentQuotas[i] && j < scripts.Count; j++)
                    {
                        scriptItems.Add(new TaskScript
                        {
                            TaskId = task.TaskId,
                            ScriptId = scripts[j],
                            AssigneeUserId = userId,
                            Status = TaskScriptStatus.Pending,
                            IncludedAt = now
                        });
                    }
                }
            }

            if (scriptItems.Count > 0) tasks.AddScriptItems(scriptItems);

            await SaveTasksAsync(ct);
        }

        // đợt vừa nhận nhiệm vụ đầu tiên thì phải rời Open.
        if (campaign is not null) await campaigns.RefreshStatusAsync(campaign.CampaignId, ct);

        var detail = await LoadDetailAsync(task.TaskId, ct);
        if (transaction is not null) await transaction.CommitAsync(ct);
        return detail;
    }

    /// <summary>Gắn task vào chiến dịch (attach/move), hoặc gỡ ra (detach) khi campaignId = null.</summary>
    public async Task<TaskDetailDto> AttachAsync(
        long taskId, long? campaignId, CancellationToken ct = default,
        long? callerId = null, bool isAdmin = false)
    {
        await using var transaction = await BeginTransactionIfNeededAsync(ct);
        var task = await LoadEditableAsync(taskId, ct);
        await EnsureCampaignOwnerAsync(task, callerId, isAdmin, ct);

        // đợt cũ cũng phải được tính lại — rút task ra khỏi đợt xong thì đợt có
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

            // bỏ luật chặn nhận thêm task vào đợt đã đóng — xem ở CreateAsync.

            // Nhiệm vụ phải cùng chủ đề với chiến dịch
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

    /// <summary>Xoá vĩnh viễn task khỏi chiến dịch. Không chặn task đã hoàn thành hoặc đã có việc.</summary>
    /// <remarks>
    /// Lượt giao, mục công việc, hàng đợi và liên kết task_recording xoá theo task (ON DELETE CASCADE).
    /// Bản ghi thu âm và lượt duyệt ở lại, task_id chuyển NULL (ON DELETE SET NULL).
    /// </remarks>
    public async Task DeleteFromCampaignAsync(
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

        await EnsureCampaignOwnerAsync(task, callerId, isAdmin, ct);

        tasks.RemoveTask(task);
        await SaveTasksAsync(ct);
        await campaigns.RefreshStatusAsync(campaignId, ct);

        if (transaction is not null) await transaction.CommitAsync(ct);
    }

    public async Task<PagedResult<TaskListItemDto>> SearchAsync(
        TaskSearchRequest request, CancellationToken ct = default)
    {
        var now = clock.GetUtcNow();

        // Truyền request.Domain xuống repository
        var (rows, total) = await tasks.SearchAsync(
            request.TaskType, request.Status, request.AssigneeId, request.Domain, request.Overdue,
            now, request.Page, request.PageSize, request.ToSort<TaskSortField>(), ct);

        return new PagedResult<TaskListItemDto>(
            [.. rows.Select(r => r.ToListItem(now))], request.Page, request.PageSize, total);
    }

    public Task<TaskDetailDto> GetAsync(long taskId, CancellationToken ct = default) =>
        LoadDetailAsync(taskId, ct);

    /// <summary>
    /// Lấy task thu âm duy nhất của chiến dịch; chưa có thì tạo với chỉ tiêu bằng cả chiến dịch.
    /// Idempotent: gọi lại không tạo task thứ hai. <paramref name="autoFill"/> tự lấp các cặp câu
    /// đã duyệt nội dung cho tới khi đủ chỉ tiêu hoặc hết câu phù hợp.
    /// </summary>
    public async Task<TaskDetailDto> GetOrCreateRecordingTaskAsync(
        long campaignId, bool autoFill, long callerId, bool isAdmin, CancellationToken ct = default)
    {
        await using var transaction = await BeginTransactionIfNeededAsync(ct);

        // Khoá dòng campaign để hai request đồng thời không tạo hai task.
        var campaign = await campaigns.GetForUpdateAsync(campaignId, ct)
            ?? throw new NotFoundException("campaign_not_found", $"Không tìm thấy chiến dịch #{campaignId}.");

        if (!campaign.AssignedTo.HasValue)
        {
            throw new UnprocessableException(
                "campaign_not_assigned_to_manager",
                $"Chiến dịch #{campaignId} chưa được giao cho Task Manager nào nên chưa nhận task. " +
                "Nhờ Admin giao chiến dịch trước.");
        }

        if (!isAdmin && campaign.AssignedTo.Value != callerId)
        {
            throw new ForbiddenException(
                "campaign_not_owned",
                $"Chiến dịch #{campaignId} đang giao cho người khác — bạn chỉ tạo được task trong chiến dịch của mình.");
        }

        var task = await tasks.GetRecordingTaskForCampaignAsync(campaignId, ct);
        var now = clock.GetUtcNow();

        if (task is null)
        {
            // Hạn cuối ngày cuối chiến dịch theo giờ Việt Nam, đúng khung trigger chấp nhận.
            var deadline = new DateTimeOffset(
                campaign.EndDate.ToDateTime(TimeOnly.MaxValue), VietnamOffset).ToUniversalTime();

            task = new WorkTask
            {
                CampaignId = campaignId,
                // Trigger trg_task_creator_assigned đòi người tạo là Task Manager được giao đợt.
                // Admin gọi endpoint này cũng phải đứng tên người phụ trách đợt.
                CreatedBy = campaign.AssignedTo.Value,
                TaskType = TaskType.Recording,
                Domain = campaign.Domain,
                Description = campaign.CampaignName,
                TargetQty = campaign.TargetQty,
                Deadline = deadline,
                Status = WorkTaskStatus.InProgress,
                CreatedAt = now
            };

            tasks.Add(task);
            await SaveTasksAsync(ct);
            await campaigns.RefreshStatusAsync(campaignId, ct);
        }

        var taskId = task.TaskId;

        if (autoFill && task.Status == WorkTaskStatus.InProgress)
        {
            var row = await tasks.GetRowAsync(taskId, ct);
            var totalItems = row?.TotalItems ?? 0;

            // AddItemsAsync chỉ nhận tối đa 1000 mục/lần (AutoFillCriteria.Count).
            while (totalItems < task.TargetQty)
            {
                var count = Math.Min(1000, task.TargetQty - totalItems);
                var added = await AddItemsAsync(
                    taskId,
                    new AddTaskItemsRequest
                    {
                        AutoFill = new AutoFillCriteria { Count = count, Domain = campaign.Domain }
                    },
                    ct, callerId, isAdmin);

                if (added.Added <= 0) break;
                totalItems += added.Added;
            }
        }

        if (transaction is not null) await transaction.CommitAsync(ct);

        return await LoadDetailAsync(taskId, ct);
    }

    public async Task<TaskDetailDto> UpdateAsync(
        long taskId, UpdateTaskRequest request, CancellationToken ct = default,
        long? callerId = null, bool isAdmin = false)
    {
        await using var transaction = await BeginTransactionIfNeededAsync(ct);
        var task = await LoadEditableAsync(taskId, ct);
        await EnsureCampaignOwnerAsync(task, callerId, isAdmin, ct);

        if (request.Description is not null) task.Description = Clean(request.Description);

        // Cập nhật chủ đề (nếu task đang gắn chiến dịch thì phải khớp chủ đề chiến dịch)
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

            // trước đây nhánh này bỏ qua khi task còn ở trạng thái `Draft`
            // (chưa giao cho ai). `Draft` đã bị bỏ, nên điều kiện được hỏi lại đúng bằng nghĩa
            // cũ: CHƯA CÓ NGƯỜI NHẬN thì cứ đặt chỉ tiêu trước, thêm mục vào sau.
            var active = await tasks.GetOpenAssignmentsAsync(taskId, ct);
            if (active.Count > 0)
            {
                if (request.TargetQty.Value < active.Count)
                {
                    throw new UnprocessableException(
                        "target_below_assignee_count",
                        $"Chỉ tiêu {request.TargetQty.Value} nhỏ hơn {active.Count} người đang nhận task. " +
                        "Gỡ bớt người hoặc đặt chỉ tiêu ít nhất bằng số người nhận.");
                }

                var quotas = SplitQuota(request.TargetQty.Value, active.Count);
                var (workable, blocked) = await CountWorkableAsync(task, active, null, ct);

                // task duyệt trong đợt được nâng chỉ tiêu trước khi có bản ghi.
                if (RequiresItemsToAssign(task) && quotas.Max() > workable)
                {
                    throw new UnprocessableException(
                        "target_exceeds_items",
                        DescribeShortfall(quotas.Max(), workable, blocked) + " Thêm mục hoặc hạ chỉ tiêu.");
                }

                // Đổi chỉ tiêu thì chia lại quota cho những người đang nhận, nếu không
                // tổng quota cũ sẽ lệch với target mới và Speaker thấy mục tiêu sai.
                for (var i = 0; i < active.Count; i++) active[i].TargetQty = quotas[i];
            }

            task.TargetQty = request.TargetQty.Value;
        }

        await SaveTasksAsync(ct);

        // Task Manager bấm tay đổi trạng thái. Giá trị này được giữ nguyên —
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
    /// dùng chung cho nút Huỷ và cho việc Task Manager chọn `Cancelled` tay,
    /// để hai đường không lệch nhau. Các mục giữ nguyên để còn dấu vết công việc; lượt giao
    /// đang hoạt động thì chuyển sang `cancelled` — task đã dừng thì giữ chỗ là vô nghĩa.
    /// </summary>
    private async Task ApplyManualStatusAsync(WorkTask task, WorkTaskStatus status, CancellationToken ct)
    {
        if (task.Status == status) return;

        task.Status = status;

        if (status == WorkTaskStatus.Cancelled)
        {
            foreach (var assignment in await tasks.GetOpenAssignmentsAsync(task.TaskId, ct))
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

        // nhóm người nhận, không phải một người. Mọi luật "câu này thuộc về
        // người nhận task" giờ áp cho bất kỳ ai trong nhóm.
        var assignees = await tasks.GetOpenAssignmentsAsync(taskId, ct);
        var assigneeIds = assignees.Select(a => a.UserId).ToArray();

        var skipped = new List<SkippedItemDto>();
        var now = clock.GetUtcNow();
        List<string> accepted;

        if (task.TaskType == TaskType.Recording)
        {
            if (hasIds)
            {
                accepted = await FilterScriptsAsync(
                    taskId, task.Domain, [.. request.Ids.Distinct()], skipped, ct);
            }
            else
            {
                accepted = [];
                var excludedIds = new HashSet<string>();
                var criteria = request.AutoFill!;
                if (criteria.Domain.HasValue && criteria.Domain != task.Domain)
                {
                    throw new UnprocessableException(
                        "auto_fill_domain_mismatch",
                        $"Chủ đề tự lấp ({criteria.Domain}) không khớp với chủ đề nhiệm vụ ({task.Domain}).");
                }

                while (accepted.Count < criteria.Count)
                {
                    var ids = await tasks.FindScriptCandidatesAsync(
                        taskId, assigneeIds, criteria.Domain ?? task.Domain,
                        criteria.Count - accepted.Count, [.. excludedIds], ct);
                    if (ids.Count == 0) break;

                    excludedIds.UnionWith(ids);
                    accepted.AddRange(await FilterScriptsAsync(
                        taskId, task.Domain, [.. ids], skipped, ct));
                }
            }

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
            if (hasIds)
            {
                accepted = await FilterRecordingsAsync(
                    taskId, [.. request.Ids.Distinct()], assigneeIds, roundsRequired, skipped, ct);
            }
            else
            {
                accepted = [];
                var excludedIds = new HashSet<string>();
                var criteria = request.AutoFill!;
                while (accepted.Count < criteria.Count)
                {
                    var ids = await tasks.FindRecordingCandidatesAsync(
                        taskId, assigneeIds, criteria.SpeakerId,
                        criteria.Count - accepted.Count, roundsRequired, [.. excludedIds], ct);
                    if (ids.Count == 0) break;

                    excludedIds.UnionWith(ids);
                    accepted.AddRange(await FilterRecordingsAsync(
                        taskId, [.. ids], assigneeIds, roundsRequired, skipped, ct));
                }
            }

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
            // Mục câu giờ có khoá thay thế: một cặp câu có thể có nhiều dòng cho nhiều
            // Speaker. Client cũ gửi script_id thì lấy dòng đầu tiên như trước.
            var item = long.TryParse(itemId, out var taskScriptId)
                ? await tasks.GetScriptItemByIdAsync(taskId, taskScriptId, ct)
                : await tasks.GetScriptItemAsync(taskId, itemId, ct);
            if (item is null) throw ItemNotFound(taskId, itemId);

            if (item.Status != TaskScriptStatus.Pending)
            {
                throw ItemNotRemovable(item.ScriptId, item.Status.ToString());
            }

            // Mục vẫn Pending trong lúc bản ghi của nó chờ duyệt — nhìn trạng thái mục thôi chưa đủ.
            // Chỉ xét bản của đúng Speaker nhận mục này; bản của Speaker khác không giữ mục.
            if (await tasks.HasSubmittedRecordingAsync(item.ScriptId, item.AssigneeUserId, ct))
            {
                throw new ConflictException(
                    "task_item_not_removable",
                    $"Cặp câu {item.ScriptId} đã có bản ghi nộp và đang chờ duyệt, không gỡ được — gỡ đi là mất dấu công việc đã làm.");
            }

            await EnsureRemovalKeepsTargetAsync(task, item.TaskScriptId.ToString(), ct);
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
    /// giao task cho NHIỀU người cùng lúc. Trước đây giao là ĐUỔI người cũ
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

        var current = await tasks.GetOpenAssignmentsAsync(taskId, ct);
        var already = current.Select(a => a.UserId).ToHashSet();

        var toAdd = wanted.Where(id => !already.Contains(id)).ToArray();

        if (toAdd.Length == 0)
        {
            // không có người mới để thêm → coi như thành công (no-op),
            // tránh ném ConflictException khi frontend gửi lại danh sách không đổi.
            await tracker.RefreshStatusAsync(taskId, ct);
            return await LoadDetailAsync(taskId, ct);
        }

        var fresh = new List<AppUser>(toAdd.Length);

        foreach (var userId in toAdd)
        {
            fresh.Add(await EnsureEligibleAssigneeAsync(userId, task.TaskType, ct));
        }

        // chỉ tiêu là của CẢ NHÓM. Mỗi người khoảng target/n, nên không
        // đòi mỗi người phải làm đủ cả target — làm vậy thì giao người thứ hai vào
        // là luôn trượt điều kiện dù tổng số mục vẫn dư.
        var finalCount = already.Count + toAdd.Length;
        if (task.TargetQty < finalCount)
        {
            throw new UnprocessableException(
                "target_below_assignee_count",
                $"Task chỉ có {task.TargetQty} chỉ tiêu nhưng đang có {finalCount} người nhận. " +
                "Nâng chỉ tiêu trước khi giao thêm người.");
        }

        var quotas = SplitQuota(task.TargetQty, finalCount);
        var afterAssigning = new List<TaskAssignment>(current);
        afterAssigning.AddRange(fresh.Select(f => new TaskAssignment { TaskId = taskId, UserId = f.UserId }));
        var (workable, blocked) = await CountWorkableAsync(task, afterAssigning, null, ct);

        // task duyệt trong đợt nhận việc qua đường tự đẩy lúc Speaker nộp, nên hàng
        // chờ rỗng lúc giao KHÔNG phải lỗi — giao reviewer trước, bản ghi tự tới sau là luồng
        // chính. Task thu âm và task duyệt ngoài đợt vẫn phải đủ mục trước khi giao.
        if (RequiresItemsToAssign(task) && quotas.Max() > workable)
        {
            throw new UnprocessableException(
                "target_exceeds_items",
                DescribeShortfall(quotas.Max(), workable, blocked) +
                $" Mỗi người cần khoảng {quotas.Max()}/{task.TargetQty} mục (chia cho {finalCount} người nhận). " +
                "Thêm mục, gỡ các mục đó, hoặc hạ chỉ tiêu.");
        }

        await using var transaction = await BeginTransactionIfNeededAsync(ct);

        var now = clock.GetUtcNow();

        // Giao thêm người là chia lại quota cho cả nhóm; người cũ cũng nhận quota mới.
        for (var i = 0; i < current.Count; i++) current[i].TargetQty = quotas[i];

        for (var i = 0; i < fresh.Count; i++)
        {
            tasks.AddAssignment(new TaskAssignment
            {
                TaskId = taskId,
                UserId = fresh[i].UserId,
                AssignedAt = now.AddTicks(current.Count + i),
                AssignmentStatus = AssignmentStatus.Active,
                TargetQty = quotas[current.Count + i]
            });
        }

        // task thu âm chia lại các cặp CHƯA bắt đầu theo quota mới.
        // Cặp đã có bản ghi/đang thu giữ nguyên người nhận.
        if (task.TaskType == TaskType.Recording)
        {
            var fillOrder = current
                .Select((a, i) => (a.UserId, Quota: quotas[i]))
                .Concat(fresh.Select((f, i) => (f.UserId, Quota: quotas[current.Count + i])))
                .ToList();

            await RedistributeRecordingScriptsAsync(taskId, fillOrder, ct);
        }

        await SaveTasksAsync(ct);

        // giao task không còn đổi trạng thái (task đã là InProgress từ lúc tạo),
        // nhưng tracker vẫn chạy để một task bị Task Manager bấm tay về Completed rồi
        // được giao thêm người sẽ tính lại đúng theo số liệu.
        await tracker.RefreshStatusAsync(taskId, ct);
        if (transaction is not null) await transaction.CommitAsync(ct);

        return await LoadDetailAsync(taskId, ct);
    }

    /// <summary>
    /// Thay danh sách người nhận đang hoạt động bằng danh sách mới và đặt quota từng người.
    /// Khác <see cref="AssignAsync"/> (chỉ giao thêm): người không còn trong danh sách bị
    /// chuyển sang `reassigned`, nên dùng được cho màn hình sửa cả bảng phân công.
    /// </summary>
    public async Task<TaskDetailDto> SetAssignmentsAsync(
        long taskId, SetTaskAssignmentsRequest request, CancellationToken ct = default,
        long? callerId = null, bool isAdmin = false)
    {
        await using var transaction = await BeginTransactionIfNeededAsync(ct);
        var task = await LoadMutableAsync(taskId, ct);
        await EnsureCampaignOwnerAsync(task, callerId, isAdmin, ct);

        var requested = request.Assignments ?? [];
        if (requested.Count == 0)
        {
            throw new UnprocessableException("assignee_required", "Phải chọn ít nhất một người nhận.");
        }

        var duplicate = requested
            .GroupBy(a => a.UserId)
            .FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
        {
            throw new UnprocessableException(
                "assignment_duplicate_user",
                $"Người #{duplicate.Key} xuất hiện nhiều lần trong danh sách giao việc.");
        }

        var quotas = ResolveAssignmentQuotas(task.TargetQty, requested);

        foreach (var item in requested)
        {
            await EnsureEligibleAssigneeAsync(item.UserId, task.TaskType, ct);
        }

        var active = await tasks.GetOpenAssignmentsAsync(taskId, ct);

        // Đếm trên đúng danh sách mới: người bị thay ra không còn giữ chỗ, người mới
        // chưa có mục nào là chuyện bình thường với task duyệt trong đợt.
        var afterAssigning = requested
            .Select(a => new TaskAssignment { TaskId = taskId, UserId = a.UserId })
            .ToList();
        var (workable, blocked) = await CountWorkableAsync(task, afterAssigning, null, ct);

        // task duyệt nhận việc qua đường tự đẩy, nên hàng chờ rỗng lúc sửa
        // phân công không phải lỗi. Chỉ task thu âm mới phải đủ cặp câu đã gán.
        if (task.TaskType == TaskType.Recording && quotas.Sum() > workable)
        {
            throw new UnprocessableException(
                "target_exceeds_items",
                DescribeShortfall(quotas.Sum(), workable, blocked) +
                " Tổng quota của những người nhận không được vượt số mục họ làm được.");
        }

        var wanted = requested.Select(a => a.UserId).ToHashSet();
        var byUser = active.ToDictionary(a => a.UserId);

        // Người bị gỡ: nhả câu đang giữ trong hàng đợi, KHÔNG đụng bản đã nộp.
        // Bản đã nộp giữ nguyên người đọc; bản của người khác không bị chặn.
        if (task.TaskType == TaskType.Recording)
        {
            var removed = active
                .Where(a => !wanted.Contains(a.UserId))
                .Select(a => a.UserId)
                .ToList();

            foreach (var userId in removed)
            {
                await queue.ReleaseClaimsAsync(userId, taskId, ct);
            }
        }

        var now = clock.GetUtcNow();
        var quotaByUser = requested
            .Select((a, i) => new { a.UserId, TargetQty = quotas[i] })
            .ToDictionary(x => x.UserId, x => x.TargetQty);

        foreach (var assignment in active)
        {
            if (wanted.Contains(assignment.UserId))
            {
                assignment.TargetQty = quotaByUser[assignment.UserId];
            }
            else
            {
                assignment.AssignmentStatus = AssignmentStatus.Reassigned;
            }
        }

        // Người mới được thêm theo đúng thứ tự trong request; cộng ticks để giữ thứ tự
        // khi GetOpenAssignmentsAsync đọc lại bằng assigned_at.
        var nextOrder = active.Count;
        foreach (var item in requested.Where(a => !byUser.ContainsKey(a.UserId)))
        {
            tasks.AddAssignment(new TaskAssignment
            {
                TaskId = taskId,
                UserId = item.UserId,
                AssignedAt = now.AddTicks(nextOrder++),
                AssignmentStatus = AssignmentStatus.Active,
                TargetQty = quotaByUser[item.UserId]
            });
        }

        if (task.TaskType == TaskType.Recording)
        {
            var fillOrder = requested
                .Select((a, i) => (a.UserId, Quota: quotas[i]))
                .ToList();

            await RedistributeRecordingScriptsAsync(taskId, fillOrder, ct);
        }

        await SaveTasksAsync(ct);
        await tracker.RefreshStatusAsync(taskId, ct);
        if (transaction is not null) await transaction.CommitAsync(ct);

        return await LoadDetailAsync(taskId, ct);
    }

    /// <summary>
    /// Chia quota cho danh sách giao việc: số gửi kèm được giữ, ô trống chia đều phần còn lại.
    /// Không còn ô trống thì tổng các quota phải đúng bằng chỉ tiêu task.
    /// </summary>
    private static int[] ResolveAssignmentQuotas(
        int targetQty, IReadOnlyList<AssignmentQuotaRequest> assignments)
    {
        if (assignments.Any(a => a.TargetQty is < 1))
        {
            throw new UnprocessableException(
                "assignee_quota_invalid", "Chỉ tiêu của mỗi người nhận phải từ 1 trở lên.");
        }

        var explicitSum = assignments.Where(a => a.TargetQty.HasValue).Sum(a => a.TargetQty!.Value);
        if (explicitSum > targetQty)
        {
            throw new UnprocessableException(
                "assignment_quota_exceeds_target",
                $"Tổng quota {explicitSum} vượt quá chỉ tiêu {targetQty} của task.");
        }

        var autoCount = assignments.Count(a => !a.TargetQty.HasValue);
        if (autoCount == 0)
        {
            if (explicitSum != targetQty)
            {
                throw new UnprocessableException(
                    "assignment_quota_not_equal_target",
                    $"Tổng quota đã đặt là {explicitSum}, phải bằng đúng chỉ tiêu {targetQty} của task.");
            }

            return [.. assignments.Select(a => a.TargetQty!.Value)];
        }

        var remaining = targetQty - explicitSum;
        if (remaining < autoCount)
        {
            throw new UnprocessableException(
                "assignment_quota_not_enough",
                $"Còn {remaining} chỉ tiêu cho {autoCount} người chưa đặt quota; mỗi người cần ít nhất 1.");
        }

        var autoQuotas = SplitQuota(remaining, autoCount);
        var result = new int[assignments.Count];
        var next = 0;

        for (var i = 0; i < assignments.Count; i++)
        {
            result[i] = assignments[i].TargetQty ?? autoQuotas[next++];
        }

        return result;
    }

    /// <summary>
    /// gỡ một người khỏi task, giữ lịch sử (lượt giao chuyển sang Reassigned).
    ///
    /// Không cho gỡ người CUỐI CÙNG khi task đã có việc: task sẽ không còn ai làm tiếp,
    /// dữ liệu đã làm thì còn nguyên nhưng không ai dùng được nữa. Muốn dừng hẳn thì dùng Huỷ.
    ///
    /// trước đây lý do cụ thể hơn là "task rơi về Draft, mà Draft không nhận việc
    /// nữa" — trạng thái `Draft` đã bị bỏ nên luật này giữ nguyên vì lý do thật của nó.
    /// </summary>
    public async Task<TaskDetailDto> UnassignAsync(
        long taskId, long userId, CancellationToken ct = default,
        long? callerId = null, bool isAdmin = false)
    {
        await using var transaction = await BeginTransactionIfNeededAsync(ct);
        var task = await LoadEditableAsync(taskId, ct);
        await EnsureCampaignOwnerAsync(task, callerId, isAdmin, ct);

        var active = await tasks.GetOpenAssignmentsAsync(taskId, ct);
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

        if (task.TaskType == TaskType.Recording)
        {
            // Nhả câu đang giữ; bản đã nộp giữ nguyên người đọc.
            await queue.ReleaseClaimsAsync(userId, taskId, ct);
        }

        mine.AssignmentStatus = AssignmentStatus.Reassigned;

        // Chia lại phần của người vừa rời đi cho những người còn lại, để tổng quota
        // vẫn bằng chỉ tiêu task và không ai bị kẹt với mục tiêu cũ.
        var remaining = active.Where(a => !ReferenceEquals(a, mine)).ToList();
        if (remaining.Count > 0 && task.TargetQty >= remaining.Count)
        {
            var quotas = SplitQuota(task.TargetQty, remaining.Count);
            for (var i = 0; i < remaining.Count; i++) remaining[i].TargetQty = quotas[i];

            if (task.TaskType == TaskType.Recording)
            {
                var fillOrder = remaining
                    .Select((a, i) => (a.UserId, Quota: quotas[i]))
                    .ToList();

                await RedistributeRecordingScriptsAsync(taskId, fillOrder, ct);
            }
        }

        await SaveTasksAsync(ct);

        await tracker.RefreshStatusAsync(taskId, ct);
        if (transaction is not null) await transaction.CommitAsync(ct);

        return await LoadDetailAsync(taskId, ct);
    }

    /// <summary>
    /// chia lại các mục câu CHƯA bắt đầu của task thu âm theo danh sách người
    /// nhận và quota mới. Mục đã có bản ghi, đã xong hoặc đang bị giữ trong hàng đợi
    /// giữ nguyên người nhận — dữ liệu đã làm không đổi chủ.
    ///
    /// Một Speaker không bao giờ nhận hai mục của cùng một cặp câu; hai Speaker khác
    /// nhau nhận cùng cặp là hợp lệ.
    /// </summary>
    private async Task RedistributeRecordingScriptsAsync(
        long taskId, IReadOnlyList<(long UserId, int Quota)> assignments, CancellationToken ct)
    {
        var items = await tasks.GetScriptItemsTrackedAsync(taskId, ct);
        if (items.Count == 0) return;

        var startedIds = await tasks.GetStartedTaskScriptIdsAsync(taskId, clock.GetUtcNow(), ct);

        var quotaByUser = assignments.ToDictionary(a => a.UserId, a => a.Quota);

        // Mục đã bắt đầu giữ nguyên người nhận và tính vào quota của họ.
        var startedByUser = new Dictionary<long, int>();
        var currentScripts = new Dictionary<long, HashSet<string>>();
        foreach (var item in items)
        {
            if (item.AssigneeUserId is not { } userId) continue;

            if (!currentScripts.TryGetValue(userId, out var scripts))
            {
                currentScripts[userId] = scripts = [];
            }

            scripts.Add(item.ScriptId);

            if (startedIds.Contains(item.TaskScriptId))
            {
                startedByUser[userId] = startedByUser.GetValueOrDefault(userId) + 1;
            }
        }

        // Giữ các mục chưa bắt đầu của người còn nhận việc, tối đa phần quota còn lại.
        // Phần dư (và mục của người bị gỡ) trôi vào danh sách tự do.
        var keptByUser = new Dictionary<long, int>();
        var free = new List<TaskScript>();
        foreach (var item in items
                     .Where(i => !startedIds.Contains(i.TaskScriptId))
                     .OrderBy(i => i.IncludedAt).ThenBy(i => i.ScriptId).ThenBy(i => i.TaskScriptId))
        {
            if (item.AssigneeUserId is { } userId && quotaByUser.TryGetValue(userId, out var quota))
            {
                var budget = Math.Max(0, quota - startedByUser.GetValueOrDefault(userId));
                if (keptByUser.GetValueOrDefault(userId) < budget)
                {
                    keptByUser[userId] = keptByUser.GetValueOrDefault(userId) + 1;
                    continue;
                }
            }

            free.Add(item);
        }

        // Không đặt mục về null hàng loạt: hai dòng cùng câu sẽ đụng
        // uq_task_script_unassigned giữa chừng. Chỉ gán thẳng cho người chưa giữ
        // câu đó — mỗi lần ghi vẫn là một khoá (task, script, speaker) duy nhất.
        var taken = new HashSet<long>();

        foreach (var (userId, quota) in assignments)
        {
            var need = Math.Max(0,
                quota - startedByUser.GetValueOrDefault(userId) - keptByUser.GetValueOrDefault(userId));
            if (!currentScripts.TryGetValue(userId, out var scripts))
            {
                currentScripts[userId] = scripts = [];
            }

            foreach (var item in free)
            {
                if (need == 0) break;
                if (taken.Contains(item.TaskScriptId) || scripts.Contains(item.ScriptId)) continue;

                item.AssigneeUserId = userId;
                scripts.Add(item.ScriptId);
                taken.Add(item.TaskScriptId);
                need--;
            }
        }

        // Mục thừa (task có nhiều mục hơn tổng quota): chia vòng tròn cho người
        // chưa có cặp đó; không còn ai hợp lệ thì để trống thay vì giao trùng.
        var next = 0;
        foreach (var item in free)
        {
            if (taken.Contains(item.TaskScriptId) || assignments.Count == 0) continue;

            for (var k = 0; k < assignments.Count; k++)
            {
                var userId = assignments[(next + k) % assignments.Count].UserId;
                if (currentScripts[userId].Contains(item.ScriptId)) continue;

                item.AssigneeUserId = userId;
                currentScripts[userId].Add(item.ScriptId);
                taken.Add(item.TaskScriptId);
                next = (next + k + 1) % assignments.Count;
                break;
            }
        }

        // Mục còn lại không ai nhận: giữ đúng một dòng trống cho mỗi câu, xoá phần dư
        // để không đụng uq_task_script_unassigned.
        foreach (var group in free
                     .Where(i => !taken.Contains(i.TaskScriptId))
                     .GroupBy(i => i.ScriptId))
        {
            var keep = group.FirstOrDefault(i => i.AssigneeUserId is null) ?? group.First();
            foreach (var item in group)
            {
                if (ReferenceEquals(item, keep))
                    item.AssigneeUserId = null;
                else
                    tasks.Remove(item);
            }
        }
    }

    /// <summary>
    /// Chia <paramref name="targetQty"/> cho <paramref name="count"/> người theo kiểu
    /// "phần nguyên + dư": người đầu nhận thêm 1 cho tới khi hết phần dư. Tổng luôn đúng
    /// bằng chỉ tiêu task; không dùng ceil vì ceil làm tổng vượt chỉ tiêu.
    /// </summary>
    private static int[] SplitQuota(int targetQty, int count)
    {
        if (count <= 0) return [];

        var baseQty = targetQty / count;
        var remainder = targetQty % count;
        var quotas = new int[count];

        for (var i = 0; i < count; i++)
        {
            quotas[i] = baseQty + (i < remainder ? 1 : 0);
        }

        return quotas;
    }

    /// <summary>
    /// quota hiển thị cho lượt giao cũ chưa có target_qty (dữ liệu trước V12).
    /// Lấy đúng phần của người ở vị trí <paramref name="index"/> trong mảng đã sắp xếp
    /// (cùng thứ tự <see cref="SplitQuota"/> dùng khi chia), để tổng các phần hiển thị
    /// đúng bằng chỉ tiêu task. Min 1 cho dữ liệu cũ bất thường (target &lt; số người),
    /// cùng công thức backfill V12.
    /// </summary>
    internal static int SplitQuotaAt(int targetQty, int count, int index)
    {
        if (count <= 0) return targetQty;

        if (index < 0 || index >= count) index = 0;
        var quotas = SplitQuota(targetQty, count);
        return Math.Max(1, quotas[index]);
    }

    /// <summary>
    /// task duyệt trong đợt nhận việc qua đường tự đẩy lúc Speaker nộp, nên hàng chờ
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

        // đi qua helper chung với đường "Task Manager chọn Cancelled tay" ở
        // UpdateAsync — một nơi ghi trạng thái, một nơi huỷ lượt giao.
        await ApplyManualStatusAsync(task, WorkTaskStatus.Cancelled, ct);

        // Các mục giữ nguyên để còn dấu vết. Task đã huỷ không giữ chỗ gì nữa:
        // truy vấn tự lấp chỉ tránh những mục đang nằm trong task còn chạy.
        if (task.CampaignId.HasValue) await campaigns.RefreshStatusAsync(task.CampaignId.Value, ct);

        return await LoadDetailAsync(taskId, ct);
    }

    // Xoá cứng task đã gỡ khỏi đợt và chưa có dữ liệu nộp thật
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

        // 3. Chặn xoá khi task đã có dữ liệu nộp/duyệt thật (bản ghi nộp về, lượt duyệt).
        //    Lượt giao và hàng chờ chỉ là sổ sách — task_assignment / task_script /
        //    task_recording xoá theo task (ON DELETE CASCADE).
        var hasSubmitted = await tasks.HasSubmittedWorkAsync(taskId, ct);
        if (hasSubmitted)
        {
            throw new ConflictException(
                "task_has_work",
                $"Nhiệm vụ #{taskId} đã có dữ liệu nộp/duyệt. Dùng chức năng Huỷ nếu không làm nữa để giữ dấu vết.");
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
            // đếm riêng phần việc của người này. Task có 3 người nhận thì
            // cả ba sẽ thấy cùng TaskRow.Done nếu không tra riêng.
            var mine = await tasks.GetAssigneeDoneAsync(row.TaskId, row.TaskType, ct);
            var nop = await tasks.GetAssigneeSubmittedAsync(row.TaskId, row.TaskType, ct);

            // V12: quota nằm trên lượt giao. Dữ liệu cũ chưa có quota thì lấy đúng phần
            // của người này trong phép chia (cùng thứ tự Assignees đã sắp xếp), để tổng
            // hiển thị đúng bằng chỉ tiêu task.
            var myIndex = row.Assignees.ToList().FindIndex(a => a.UserId == speakerId);
            var myTarget = row.Assignees.FirstOrDefault(a => a.UserId == speakerId)?.TargetQty
                           ?? SplitQuotaAt(row.TargetQty, row.Assignees.Count, myIndex);

            active.Add(row.ToSpeakerActiveTask(
                now, mine.GetValueOrDefault(speakerId), nop.GetValueOrDefault(speakerId), myTarget));
        }

        var (submitted, approved) = await tasks.GetSpeakerTotalsAsync(speakerId, ct);

        return new SpeakerProgressDto(submitted, approved, active);
    }

    public async Task<IReadOnlyList<AssignableUserDto>> GetAssignableUsersAsync(
        TaskType taskType, CancellationToken ct = default)
    {
        var role = taskType == TaskType.Recording ? RoleName.Speaker : RoleName.Reviewer;

        // rows arrive ranked-first from repository
        // (approval DESC, overdue ASC, load ASC); keep that order for Task Manager UI.
        var rows = await tasks.GetAssignableUsersAsync(role, clock.GetUtcNow(), ct);

        return [.. rows.Select(r => new AssignableUserDto(
            r.UserId, r.FullName, r.Email, r.Phone, r.Role, r.ActiveTasks,
            r.TotalTarget, r.ApprovalRatePct, r.OverdueTasks))];
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
        // chỉ còn một trạng thái "đang chạy" là InProgress, nên không cộng
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

    /// <returns>Danh sách id được nhận vào task.</returns>
    private async Task<List<string>> FilterScriptsAsync(
        long taskId, ScriptDomain taskDomain, string[] ids, List<SkippedItemDto> skipped,
        CancellationToken ct)
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
            else if (script.Domain != taskDomain)
                skipped.Add(new SkippedItemDto(id, $"Chủ đề câu ({script.Domain}) không khớp với chủ đề nhiệm vụ ({taskDomain})."));
            // Không còn chặn vì "người đọc khác đã thu": nhiều Speaker được dùng chung
            // một cặp câu, mỗi người có bản ghi riêng.
            else
                accepted.Add(id);
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
            // bản ghi của bất kỳ ai trong nhóm nhận task đều không thể đưa
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
        // cùng nghĩa với nhánh `Draft` cũ — chưa giao cho ai thì chưa có mục nào
        // để kiểm tra, cứ để Task Manager sửa tiếp.
        var active = await tasks.GetOpenAssignmentsAsync(task.TaskId, ct);
        if (active.Count == 0) return;

        var (workable, blocked) = await CountWorkableAsync(task, active, itemId, ct);

        // task duyệt trong đợt tự đầy lại khi có bản ghi mới, nên gỡ một mục
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
    /// trước đây đếm theo MỘT người nhận. Nhiều người thì phải hỏi "cả nhóm
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
    /// Người nhận task phải đang hoạt động và đúng vai: Speaker cho task thu âm,
    /// Reviewer cho task duyệt. Dùng chung cho tạo task, giao thêm và sửa phân công.
    /// </summary>
    private async Task<AppUser> EnsureEligibleAssigneeAsync(
        long userId, TaskType taskType, CancellationToken ct)
    {
        var requiredRole = taskType == TaskType.Recording ? RoleName.Speaker : RoleName.Reviewer;
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
                $"Task {taskType} chỉ giao được cho {requiredRole}, còn {user.Email} là {user.Role.RoleName}.");
        }

        return user;
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
                new TaskItemDto(
                    i.ScriptId, i.Status.ToString(), Preview(i.Script.CsContent),
                    i.IncludedAt, i.AssigneeUserId, i.TaskScriptId))]
            : [.. (await tasks.GetRecordingItemsAsync(taskId, ct)).Select(i =>
                new TaskItemDto(
                    i.RecordingId,
                    i.Status.ToString(),
                    $"{i.Recording.SentenceVariant} · {i.Recording.Status} · {i.Recording.DurationSec:0.##} giây",
                    i.IncludedAt))];

        return new TaskDetailDto(
            row.ToListItem(now),
            [.. history.Select(a => new TaskAssignmentDto(
                a.AssignmentId, a.UserId, a.User.FullName, a.TargetQty, a.AssignmentStatus, a.AssignedAt))],
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
