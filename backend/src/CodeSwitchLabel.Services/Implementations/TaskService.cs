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
    /// Task Manager đếm việc đã ĐƯỢC DUYềE xong; Speaker đếm việc đã THU xong. Cùng một task
    /// nhưng hai bềEsềEkhác nhau, nên chềEnào muốn bềEnào thì truyền bềEđó vào.
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
        if (r.WaveId.HasValue)
            return (false, $"Nhiệm vụ đang nằm trong đợt \"{r.WaveName ?? ("#" + r.WaveId)}\". Hãy gỡ khỏi đợt trước khi xoá.");

        if (r.Status == WorkTaskStatus.Completed)
            return (false, "Nhiệm vụ đã hoàn thành, không xoá được.");

        if (r.Started > 0)
            return (false, "Nhiệm vụ đã có dữ liệu nộp/duyệt. Dùng chức năng Huỷ đềEgiữ dấu vết.");

        return (true, null);
    }

    public static TaskListItemDto ToListItem(this TaskRow r, DateTimeOffset now)
    {
        var (canDelete, reason) = r.EvaluateCanDelete();
        return new(r.TaskId,
            r.WaveId,
            r.WaveName,
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
    // Tiến đềEđếm cặp câu đã THU ĐỦ, không đợi duyệt: gửi xong hai bản là cộng 1.
    //
    // task có thềEgiao cho nhiều người, nên trả CẢ HAI bềEsềE
    //   MyDone    Eviệc của riêng người đang xem (đềEbiết mình còn bao nhiêu)
    //   TeamDone  Eviệc của cả nhóm (đềEbiết cả đợt còn bao nhiêu)
    // ChềEtrả một bềEthì hoặc người đó tưởng mình làm cả nhiệm vụ, hoặc tưởng nhóm
    // mình không làm gì cả.
    public static SpeakerActiveTaskDto ToSpeakerActiveTask(
        this TaskRow r, DateTimeOffset now, int myDone, int mySubmitted, int myTargetQty) =>
        new(r.TaskId,
            r.Description,
            r.Description,
            r.WaveName,
            r.TaskType,
            r.Status,
            r.Deadline,
            // Cả 'done' lẫn 'submitted' đều phải là của riêng người xem. Trước đây chềEđè
            // 'done', nên task có ba người nhận thì cả ba cùng thấy tổng sềEbản ghi của nhóm
            // nằm trong myProgress  Eai cũng tưởng mình nộp bằng đó.
            //
            // V12: chềEtiêu của riêng người nhận nằm ềEtask_assignment.target_qty, không
            // còn lấy chềEtiêu của cả nhóm cho myProgress.
            r.ProgressOf(now, myDone, mySubmitted, myTargetQty),
            r.ProgressOf(now, r.SubmittedPairs),
            [.. r.Assignees.Select(a => new TaskAssigneeDto(a.UserId, a.FullName, a.TargetQty))]);
}

public class TaskService(
    ITaskRepository tasks,
    IWaveRepository waves,
    IScriptQueueRepository queue,
    ITaskProgressTracker tracker,
    ISystemConfigService config,
    TimeProvider clock) : ITaskService
{
    // Múi giềEViệt Nam (UTC+7 / Asia/Ho_Chi_Minh) thống nhất với database
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
            // ChềEtiêu phải >= sềEngười nhận
            if (targetQty < assigneeUserIds.Count)
            {
                throw new UnprocessableException(
                    "target_less_than_assignees",
                    $"ChềEtiêu {targetQty} nhềEhơn sềEngười nhận {assigneeUserIds.Count}. Mỗi người nhận cần ít nhất 1 mục.");
            }

            // Chia quota: phần dư vềEngười được chọn trước (cùng công thức SplitQuota)
            assignmentQuotas = SplitQuota(targetQty, assigneeUserIds.Count);

            // Xác thực từng thành viên
            foreach (var userId in assigneeUserIds)
            {
                await EnsureEligibleAssigneeAsync(userId, request.TaskType!.Value, ct);
            }
        }

        // waveId null = task độc lập (C1): bềEqua mọi kiểm tra chiến dịch.
        // Trigger trg_task_wave_window / trg_task_wave_target / trg_task_creator_assigned
        // đều RETURN NEW ngay khi NEW.wave_id IS NULL.
        Wave? wave = null;
        if (request.WaveId.HasValue)
        {
            var waveId = request.WaveId.Value;

            wave = await waves.GetAsync(waveId, ct)
                ?? throw new NotFoundException("wave_not_found", $"Không tìm thấy chiến dịch #{waveId}.");

            // ERD  ETask Manager chềEtạo được task trong chiến dịch Admin đã giao cho chính
            // mình; chiến dịch chưa giao thì chưa nhận task. Trigger trg_task_creator_assigned của
            // database chặn lần nữa ềEtầng sâu.
            if (wave.AssignedTo != createdById)
            {
                throw new UnprocessableException(
                    "wave_not_assigned_to_manager",
                    wave.AssignedTo is null
                        ? $"Chiến dịch #{waveId} chưa được giao cho Task Manager nào nên chưa nhận task. NhềEAdmin giao chiến dịch trước."
                        : $"Chiến dịch #{waveId} đang giao cho người khác  Ebạn chềEtạo được task trong chiến dịch của mình.");
            }

            // bềEluật "đợt đã đóng thì không nhận thêm task". Trước đây đợt `Completed`
            // nghĩa là đã chốt kết quả; giềEnó chềEcó nghĩa là mọi task đã xong, và gắn task mới
            // vào thì đợt tự quay lại `InProgress`. Chặn ềEđây sẽ thành vòng luẩn quanh: đợt xong
            // ↁEkhông gắn được ↁEkhông bao giềEmềElại được nữa.

            // Hạn task phải nằm trong khoảng ngày của chiến dịch (trigger của database cũng chặn).
            var deadlineDate = ToVietnamDate(deadline);

            if (deadlineDate < wave.StartDate || deadlineDate > wave.EndDate)
            {
                throw new UnprocessableException(
                    "task_deadline_outside_wave",
                    $"Hạn {deadlineDate:yyyy-MM-dd} phải nằm trong khoảng ngày của chiến dịch " +
                    $"{wave.StartDate:yyyy-MM-dd} đến {wave.EndDate:yyyy-MM-dd}.");
            }

            var domain = request.Domain ?? wave.Domain;

            // Nhiệm vụ phải cùng chủ đềEvới chiến dịch
            if (domain != wave.Domain)
            {
                throw new UnprocessableException(
                    "task_domain_mismatch_wave",
                    $"Chủ đềEnhiệm vụ ({domain}) không khớp với chủ đềEcủa chiến dịch #{waveId} ({wave.Domain}).");
            }

            // Tổng chềEtiêu các task không được vượt chềEtiêu chiến dịch.
            var allocated = await waves.SumAllocatedAsync(waveId, ct);

            if (allocated + targetQty > wave.TargetQty)
            {
                throw new UnprocessableException(
                    "task_target_exceeds_wave",
                    $"Chiến dịch #{waveId} đã chia {allocated}/{wave.TargetQty} cặp câu; " +
                    $"thêm {targetQty} là vượt chềEtiêu.");
            }
        }

        var taskDomain = request.Domain ?? (wave != null ? wave.Domain : ScriptDomain.ItTechnology);

        // Task thu âm: lấy danh sách câu RIÊNG cho từng Speaker. Hai Speaker được nhận
        // cùng một câu, nên kho câu ít hơn chềEtiêu vẫn tạo task  Emỗi người chềEnhận
        // tối đa sềEcâu mình còn thu được.
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
            WaveId = request.WaveId,
            CreatedBy = createdById,
            TaskType = request.TaskType!.Value,
            Domain = taskDomain, // TEAM_007
            Description = Clean(request.Description),
            TargetQty = targetQty,
            Deadline = deadline,
            // còn ba trạng thái (InProgress/Completed/Cancelled). Task vừa tạo chưa
            // có việc gì nhưng KHÔNG còn trạng thái nháp nữa  EchềEngười nhận thì vẫn là
            // InProgress, và chềEtiêu vẫn được đặt trước rồi thêm mục vào sau.
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

                // Cộng ticks đềEthứ tự chọn được giữ nguyên khi đọc lại (ORDER BY assigned_at).
                tasks.AddAssignment(new TaskAssignment
                {
                    TaskId = task.TaskId,
                    UserId = userId,
                    AssignedAt = now.AddTicks(i),
                    AssignmentStatus = AssignmentStatus.Active,
                    TargetQty = assignmentQuotas![i]
                });

                // Recording: mỗi người nhận các câu CHÍNH HềEchưa thu, tối đa quota.
                // Danh sách của hai Speaker có thềEtrùng nhau  Eđó là chủ ý.
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
        if (wave is not null) await waves.RefreshStatusAsync(wave.WaveId, ct);

        var detail = await LoadDetailAsync(task.TaskId, ct);
        if (transaction is not null) await transaction.CommitAsync(ct);
        return detail;
    }

    /// <summary>Gắn task vào chiến dịch (attach/move), hoặc gỡ ra (detach) khi waveId = null.</summary>
    public async Task<TaskDetailDto> AttachAsync(
        long taskId, long? waveId, CancellationToken ct = default,
        long? callerId = null, bool isAdmin = false)
    {
        await using var transaction = await BeginTransactionIfNeededAsync(ct);
        var task = await LoadEditableAsync(taskId, ct);
        await EnsureWaveOwnerAsync(task, callerId, isAdmin, ct);

        // đợt cũ cũng phải được tính lại  Erút task ra khỏi đợt xong thì đợt có
        // thềEhết nhiệm vụ (vềElại Open) hoặc mọi task còn lại đều xong (vềECompleted).
        var previousWaveId = task.WaveId;

        if (waveId.HasValue)
        {
            var wave = await waves.GetAsync(waveId.Value, ct)
                ?? throw new NotFoundException("wave_not_found", $"Không tìm thấy chiến dịch #{waveId}.");

            if (wave.AssignedTo.HasValue && callerId.HasValue && !isAdmin
                && wave.AssignedTo.Value != callerId.Value)
            {
                throw new ForbiddenException(
                    "task_not_owned",
                    $"Chiến dịch #{waveId} không giao cho bạn nên không gắn task vào được.");
            }

            // bềEluật chặn nhận thêm task vào đợt đã đóng  Exem ềECreateAsync.

            // Nhiệm vụ phải cùng chủ đềEvới chiến dịch
            if (task.Domain != wave.Domain)
            {
                throw new UnprocessableException(
                    "task_domain_mismatch_wave",
                    $"Chủ đềEnhiệm vụ ({task.Domain}) không khớp với chủ đềEcủa chiến dịch #{waveId.Value} ({wave.Domain}).");
            }

            if (task.Deadline.HasValue)
            {
                var deadlineDate = ToVietnamDate(task.Deadline.Value);
                if (deadlineDate < wave.StartDate || deadlineDate > wave.EndDate)
                {
                    throw new UnprocessableException(
                        "task_deadline_outside_wave",
                        $"Hạn {deadlineDate:yyyy-MM-dd} phải nằm trong khoảng ngày của chiến dịch " +
                        $"{wave.StartDate:yyyy-MM-dd} đến {wave.EndDate:yyyy-MM-dd}.");
                }
            }

            var allocated = await waves.SumAllocatedAsync(waveId.Value, ct);
            var withoutThis = task.WaveId == waveId.Value ? allocated - task.TargetQty : allocated;
            if (withoutThis + task.TargetQty > wave.TargetQty)
            {
                throw new UnprocessableException(
                    "task_target_exceeds_wave",
                    $"Chiến dịch #{waveId} đã chia {allocated}/{wave.TargetQty} cặp câu; " +
                    $"gắn task này ({task.TargetQty}) là vượt chềEtiêu.");
            }
        }

        task.WaveId = waveId;
        await SaveTasksAsync(ct);

        if (waveId.HasValue) await waves.RefreshStatusAsync(waveId.Value, ct);
        if (previousWaveId.HasValue && previousWaveId != waveId)
        {
            await waves.RefreshStatusAsync(previousWaveId.Value, ct);
        }

        if (transaction is not null) await transaction.CommitAsync(ct);

        return await LoadDetailAsync(taskId, ct);
    }

    /// <summary>Xoá vĩnh viềE task khỏi chiến dịch. Không chặn task đã hoàn thành hoặc đã có việc.</summary>
    /// <remarks>
    /// Lượt giao, mục công việc, hàng đợi và liên kết task_recording xoá theo task (ON DELETE CASCADE).
    /// Bản ghi thu âm và lượt duyệt ềElại, task_id chuyển NULL (ON DELETE SET NULL).
    /// </remarks>
    public async Task DeleteFromWaveAsync(
        long waveId, long taskId, CancellationToken ct = default,
        long? callerId = null, bool isAdmin = false)
    {
        await using var transaction = await BeginTransactionIfNeededAsync(ct);
        var task = await tasks.GetForUpdateAsync(taskId, ct) ?? throw NotFound(taskId);

        if (task.WaveId != waveId)
        {
            throw new NotFoundException(
                "task_not_found", $"Task #{taskId} không thuộc chiến dịch #{waveId}.");
        }

        await EnsureWaveOwnerAsync(task, callerId, isAdmin, ct);

        tasks.RemoveTask(task);
        await SaveTasksAsync(ct);
        await waves.RefreshStatusAsync(waveId, ct);

        if (transaction is not null) await transaction.CommitAsync(ct);
    }

    public async Task<PagedResult<TaskListItemDto>> SearchAsync(
        TaskSearchRequest request, long? callerId = null, bool isAdmin = false, CancellationToken ct = default)
    {
        var now = clock.GetUtcNow();

        // Truyền request.Domain xuống repository
        var (rows, total) = await tasks.SearchAsync(
            request.TaskType, request.Status, request.AssigneeId, request.Domain, request.Overdue,
            now, request.Page, request.PageSize, request.ToSort<TaskSortField>(),
            callerId, isAdmin, ct);

        return new PagedResult<TaskListItemDto>(
            [.. rows.Select(r => r.ToListItem(now))], request.Page, request.PageSize, total);
    }

    public async Task<TaskDetailDto> GetAsync(
        long taskId, long? callerId = null, bool isAdmin = false, CancellationToken ct = default)
    {
        var task = await tasks.GetForUpdateAsync(taskId, ct) ?? throw NotFound(taskId);
        await EnsureWaveOwnerAsync(task, callerId, isAdmin, ct);
        return await LoadDetailAsync(taskId, ct);
    }

    /// <summary>
    /// Lấy task thu âm duy nhất của chiến dịch; chưa có thì tạo với chềEtiêu bằng cả chiến dịch.
    /// Idempotent: gọi lại không tạo task thứ hai. <paramref name="autoFill"/> tự lấp các cặp câu
    /// đã duyệt nội dung cho tới khi đủ chềEtiêu hoặc hết câu phù hợp.
    /// </summary>
    public async Task<TaskDetailDto> GetOrCreateRecordingTaskAsync(
        long waveId, bool autoFill, long callerId, bool isAdmin, CancellationToken ct = default)
    {
        await using var transaction = await BeginTransactionIfNeededAsync(ct);

        // Khoá dòng wave đềEhai request đồng thời không tạo hai task.
        var wave = await waves.GetForUpdateAsync(waveId, ct)
            ?? throw new NotFoundException("wave_not_found", $"Không tìm thấy chiến dịch #{waveId}.");

        if (!wave.AssignedTo.HasValue)
        {
            throw new UnprocessableException(
                "wave_not_assigned_to_manager",
                $"Chiến dịch #{waveId} chưa được giao cho Task Manager nào nên chưa nhận task. " +
                "NhềEAdmin giao chiến dịch trước.");
        }

        if (!isAdmin && wave.AssignedTo.Value != callerId)
        {
            throw new ForbiddenException(
                "wave_not_owned",
                $"Chiến dịch #{waveId} đang giao cho người khác  Ebạn chềEtạo được task trong chiến dịch của mình.");
        }

        var task = await tasks.GetRecordingTaskForWaveAsync(waveId, ct);
        var now = clock.GetUtcNow();

        if (task is null)
        {
            // Hạn cuối ngày cuối chiến dịch theo giềEViệt Nam, đúng khung trigger chấp nhận.
            var deadline = new DateTimeOffset(
                wave.EndDate.ToDateTime(TimeOnly.MaxValue), VietnamOffset).ToUniversalTime();

            task = new WorkTask
            {
                WaveId = waveId,
                // Trigger trg_task_creator_assigned đòi người tạo là Task Manager được giao đợt.
                // Admin gọi endpoint này cũng phải đứng tên người phụ trách đợt.
                CreatedBy = wave.AssignedTo.Value,
                TaskType = TaskType.Recording,
                Domain = wave.Domain,
                Description = wave.WaveName,
                TargetQty = wave.TargetQty,
                Deadline = deadline,
                Status = WorkTaskStatus.InProgress,
                CreatedAt = now
            };

            tasks.Add(task);
            await SaveTasksAsync(ct);
            await waves.RefreshStatusAsync(waveId, ct);
        }

        var taskId = task.TaskId;

        if (autoFill && task.Status == WorkTaskStatus.InProgress)
        {
            var row = await tasks.GetRowAsync(taskId, ct);
            var totalItems = row?.TotalItems ?? 0;

            // AddItemsAsync chềEnhận tối đa 1000 mục/lần (AutoFillCriteria.Count).
            while (totalItems < task.TargetQty)
            {
                var count = Math.Min(1000, task.TargetQty - totalItems);
                var added = await AddItemsAsync(
                    taskId,
                    new AddTaskItemsRequest
                    {
                        AutoFill = new AutoFillCriteria { Count = count, Domain = wave.Domain }
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
        await EnsureWaveOwnerAsync(task, callerId, isAdmin, ct);

        if (request.Description is not null) task.Description = Clean(request.Description);

        // Cập nhật chủ đềE(nếu task đang gắn chiến dịch thì phải khớp chủ đềEchiến dịch)
        if (request.Domain.HasValue)
        {
            var newDomain = request.Domain.Value;
            if (task.WaveId.HasValue)
            {
                var wave = await waves.GetAsync(task.WaveId.Value, ct);
                if (wave is not null && wave.Domain != newDomain)
                {
                    throw new UnprocessableException(
                        "task_domain_mismatch_wave",
                        $"Chủ đềEnhiệm vụ ({newDomain}) không khớp với chủ đềEcủa chiến dịch #{task.WaveId.Value} ({wave.Domain}).");
                }
            }
            task.Domain = newDomain;
        }

        if (request.Deadline.HasValue)
        {
            var deadline = ToUtcFuture(request.Deadline.Value, clock.GetUtcNow());
            var wave = task.WaveId.HasValue
                ? await waves.GetAsync(task.WaveId.Value, ct)
                : null;
            if (wave is not null)
            {
                var deadlineDate = ToVietnamDate(deadline);
                if (deadlineDate < wave.StartDate || deadlineDate > wave.EndDate)
                {
                    throw new UnprocessableException(
                        "task_deadline_outside_wave",
                        $"Hạn {deadlineDate:yyyy-MM-dd} phải nằm trong khoảng ngày của chiến dịch " +
                        $"{wave.StartDate:yyyy-MM-dd} đến {wave.EndDate:yyyy-MM-dd}.");
                }
            }
            task.Deadline = deadline;
        }

        if (request.TargetQty.HasValue)
        {
            // Giữ quota chiến dịch như lúc Create: tổng đã chia (trừ task này) + chềEtiêu mới <= target.
            // Task độc lập (wave_id NULL) bềEqua kiểm tra này; trigger cũng RETURN NEW ngay.
            var wave = task.WaveId.HasValue
                ? await waves.GetAsync(task.WaveId.Value, ct)
                : null;
            if (wave is not null)
            {
                var allocated = await waves.SumAllocatedAsync(task.WaveId!.Value, ct);
                var withoutThis = allocated - task.TargetQty;
                if (withoutThis + request.TargetQty.Value > wave.TargetQty)
                {
                    throw new UnprocessableException(
                        "task_target_exceeds_wave",
                        $"Chiến dịch #{wave.WaveId} đã chia {allocated}/{wave.TargetQty} cặp câu; " +
                        $"đổi task này thành {request.TargetQty.Value} là vượt chềEtiêu.");
                }
            }

            // trước đây nhánh này bềEqua khi task còn ềEtrạng thái `Draft`
            // (chưa giao cho ai). `Draft` đã bềEbềE nên điều kiện được hỏi lại đúng bằng nghĩa
            // cũ: CHƯA CÁENGƯỜI NHẬN thì cứ đặt chềEtiêu trước, thêm mục vào sau.
            var active = await tasks.GetOpenAssignmentsAsync(taskId, ct);
            if (active.Count > 0)
            {
                if (request.TargetQty.Value < active.Count)
                {
                    throw new UnprocessableException(
                        "target_below_assignee_count",
                        $"ChềEtiêu {request.TargetQty.Value} nhềEhơn {active.Count} người đang nhận task. " +
                        "Gỡ bớt người hoặc đặt chềEtiêu ít nhất bằng sềEngười nhận.");
                }

                var quotas = SplitQuota(request.TargetQty.Value, active.Count);
                var (workable, blocked) = await CountWorkableAsync(task, active, null, ct);

                // task duyệt trong đợt được nâng chềEtiêu trước khi có bản ghi.
                if (RequiresItemsToAssign(task) && quotas.Max() > workable)
                {
                    throw new UnprocessableException(
                        "target_exceeds_items",
                        DescribeShortfall(quotas.Max(), workable, blocked) + " Thêm mục hoặc hạ chềEtiêu.");
                }

                // Đổi chềEtiêu thì chia lại quota cho những người đang nhận, nếu không
                // tổng quota cũ sẽ lệch với target mới và Speaker thấy mục tiêu sai.
                for (var i = 0; i < active.Count; i++) active[i].TargetQty = quotas[i];
            }

            task.TargetQty = request.TargetQty.Value;
        }

        await SaveTasksAsync(ct);

        // Task Manager bấm tay đổi trạng thái. Giá trềEnày được giữ nguyên  E
        // cềEý KHÔNG gọi tracker ềEđây, vì tracker sẽ tính lại từ sềEliệu và ghi đè mất
        // lựa chọn của người ngay lập tức. Trạng thái bấm tay chềEbềEtính lại khi có việc
        // thật xảy ra sau đó (nộp bản ghi, duyệt, đổi chềEtiêu...).
        if (request.Status.HasValue) await ApplyManualStatusAsync(task, request.Status.Value, ct);

        // ChềEđổi CHềETIÊU mới là việc thật có ảnh hưởng tới trạng thái: nâng thì task
        // đang xong phải mềElại, hạ thì task có thềExong ngay.
        //
        // Trước TEAM_013, tracker chạy ềEMỌI lần PATCH  Enên chềEsửa mô tả cũng làm trạng
        // thái Task Manager vừa bấm tay biến mất. Sửa tên, đổi hạn, đổi chủ đềEđều không
        // đụng tới sềEliệu nên không có lý do gì đềEtính lại.
        if (!request.Status.HasValue && request.TargetQty.HasValue)
        {
            await tracker.RefreshStatusAsync(taskId, ct);
        }

        if (task.WaveId.HasValue) await waves.RefreshStatusAsync(task.WaveId.Value, ct);

        if (transaction is not null) await transaction.CommitAsync(ct);

        return await LoadDetailAsync(taskId, ct);
    }

    /// <summary>
    /// dùng chung cho nút Huỷ và cho việc Task Manager chọn `Cancelled` tay,
    /// đềEhai đường không lệch nhau. Các mục giữ nguyên đềEcòn dấu vết công việc; lượt giao
    /// đang hoạt động thì chuyển sang `cancelled`  Etask đã dừng thì giữ chềElà vô nghĩa.
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
        await EnsureWaveOwnerAsync(task, callerId, isAdmin, ct);

        var hasIds = request.Ids.Count > 0;
        var hasAutoFill = request.AutoFill is not null;

        if (hasIds == hasAutoFill)
        {
            throw new UnprocessableException(
                "items_source_required",
                "Chọn đúng một cách: gửi danh sách ids, hoặc gửi điều kiện autoFill.");
        }

        // nhóm người nhận, không phải một người. Mọi luật "câu này thuộc vềE
        // người nhận task" giềEáp cho bất kỳ ai trong nhóm.
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
                        $"Chủ đềEtự lấp ({criteria.Domain}) không khớp với chủ đềEnhiệm vụ ({task.Domain}).");
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
        await EnsureWaveOwnerAsync(task, callerId, isAdmin, ct);

        if (task.TaskType == TaskType.Recording)
        {
            // Mục câu giềEcó khoá thay thế: một cặp câu có thềEcó nhiều dòng cho nhiều
            // Speaker. Client cũ gửi script_id thì lấy dòng đầu tiên như trước.
            var item = long.TryParse(itemId, out var taskScriptId)
                ? await tasks.GetScriptItemByIdAsync(taskId, taskScriptId, ct)
                : await tasks.GetScriptItemAsync(taskId, itemId, ct);
            if (item is null) throw ItemNotFound(taskId, itemId);

            if (item.Status != TaskScriptStatus.Pending)
            {
                throw ItemNotRemovable(item.ScriptId, item.Status.ToString());
            }

            // Mục vẫn Pending trong lúc bản ghi của nó chềEduyệt  Enhìn trạng thái mục thôi chưa đủ.
            // ChềExét bản của đúng Speaker nhận mục này; bản của Speaker khác không giữ mục.
            if (await tasks.HasSubmittedRecordingAsync(item.ScriptId, item.AssigneeUserId, ct))
            {
                throw new ConflictException(
                    "task_item_not_removable",
                    $"Cặp câu {item.ScriptId} đã có bản ghi nộp và đang chềEduyệt, không gỡ được  Egỡ đi là mất dấu công việc đã làm.");
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
    /// (chuyển lượt giao của hềEsang Reassigned)  Eđúng khi mỗi task chềEmột người,
    /// nhưng sai ngay khi cho phép nhiều người. GiềEgiao thêm, không đuổi ai.
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
        await EnsureWaveOwnerAsync(task, callerId, isAdmin, ct);

        var current = await tasks.GetOpenAssignmentsAsync(taskId, ct);
        var already = current.Select(a => a.UserId).ToHashSet();

        var toAdd = wanted.Where(id => !already.Contains(id)).ToArray();

        if (toAdd.Length == 0)
        {
            // không có người mới đềEthêm ↁEcoi như thành công (no-op),
            // tránh ném ConflictException khi frontend gửi lại danh sách không đổi.
            await tracker.RefreshStatusAsync(taskId, ct);
            return await LoadDetailAsync(taskId, ct);
        }

        var fresh = new List<AppUser>(toAdd.Length);

        foreach (var userId in toAdd)
        {
            fresh.Add(await EnsureEligibleAssigneeAsync(userId, task.TaskType, ct));
        }

        // chềEtiêu là của CẢ NHÓM. Mỗi người khoảng target/n, nên không
        // đòi mỗi người phải làm đủ cả target  Elàm vậy thì giao người thứ hai vào
        // là luôn trượt điều kiện dù tổng sềEmục vẫn dư.
        var finalCount = already.Count + toAdd.Length;
        if (task.TargetQty < finalCount)
        {
            throw new UnprocessableException(
                "target_below_assignee_count",
                $"Task chềEcó {task.TargetQty} chềEtiêu nhưng đang có {finalCount} người nhận. " +
                "Nâng chềEtiêu trước khi giao thêm người.");
        }

        var quotas = SplitQuota(task.TargetQty, finalCount);
        var afterAssigning = new List<TaskAssignment>(current);
        afterAssigning.AddRange(fresh.Select(f => new TaskAssignment { TaskId = taskId, UserId = f.UserId }));
        var (workable, blocked) = await CountWorkableAsync(task, afterAssigning, null, ct);

        // task duyệt trong đợt nhận việc qua đường tự đẩy lúc Speaker nộp, nên hàng
        // chềErỗng lúc giao KHÔNG phải lỗi  Egiao reviewer trước, bản ghi tự tới sau là luồng
        // chính. Task thu âm và task duyệt ngoài đợt vẫn phải đủ mục trước khi giao.
        if (RequiresItemsToAssign(task) && quotas.Max() > workable)
        {
            throw new UnprocessableException(
                "target_exceeds_items",
                DescribeShortfall(quotas.Max(), workable, blocked) +
                $" Mỗi người cần khoảng {quotas.Max()}/{task.TargetQty} mục (chia cho {finalCount} người nhận). " +
                "Thêm mục, gỡ các mục đó, hoặc hạ chềEtiêu.");
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
        // nhưng tracker vẫn chạy đềEmột task bềETask Manager bấm tay vềECompleted rồi
        // được giao thêm người sẽ tính lại đúng theo sềEliệu.
        await tracker.RefreshStatusAsync(taskId, ct);
        if (transaction is not null) await transaction.CommitAsync(ct);

        return await LoadDetailAsync(taskId, ct);
    }

    /// <summary>
    /// Thay danh sách người nhận đang hoạt động bằng danh sách mới và đặt quota từng người.
    /// Khác <see cref="AssignAsync"/> (chềEgiao thêm): người không còn trong danh sách bềE
    /// chuyển sang `reassigned`, nên dùng được cho màn hình sửa cả bảng phân công.
    /// </summary>
    public async Task<TaskDetailDto> SetAssignmentsAsync(
        long taskId, SetTaskAssignmentsRequest request, CancellationToken ct = default,
        long? callerId = null, bool isAdmin = false)
    {
        await using var transaction = await BeginTransactionIfNeededAsync(ct);
        var task = await LoadMutableAsync(taskId, ct);
        await EnsureWaveOwnerAsync(task, callerId, isAdmin, ct);

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

        // Đếm trên đúng danh sách mới: người bềEthay ra không còn giữ chềE người mới
        // chưa có mục nào là chuyện bình thường với task duyệt trong đợt.
        var afterAssigning = requested
            .Select(a => new TaskAssignment { TaskId = taskId, UserId = a.UserId })
            .ToList();
        var (workable, blocked) = await CountWorkableAsync(task, afterAssigning, null, ct);

        // task duyệt nhận việc qua đường tự đẩy, nên hàng chềErỗng lúc sửa
        // phân công không phải lỗi. ChềEtask thu âm mới phải đủ cặp câu đã gán.
        if (task.TaskType == TaskType.Recording && quotas.Sum() > workable)
        {
            throw new UnprocessableException(
                "target_exceeds_items",
                DescribeShortfall(quotas.Sum(), workable, blocked) +
                " Tổng quota của những người nhận không được vượt sềEmục hềElàm được.");
        }

        var wanted = requested.Select(a => a.UserId).ToHashSet();
        var byUser = active.ToDictionary(a => a.UserId);

        // Người bềEgỡ: nhả câu đang giữ trong hàng đợi, KHÔNG đụng bản đã nộp.
        // Bản đã nộp giữ nguyên người đọc; bản của người khác không bềEchặn.
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

        // Người mới được thêm theo đúng thứ tự trong request; cộng ticks đềEgiữ thứ tự
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
    /// Chia quota cho danh sách giao việc: sềEgửi kèm được giữ, ô trống chia đều phần còn lại.
    /// Không còn ô trống thì tổng các quota phải đúng bằng chềEtiêu task.
    /// </summary>
    private static int[] ResolveAssignmentQuotas(
        int targetQty, IReadOnlyList<AssignmentQuotaRequest> assignments)
    {
        if (assignments.Any(a => a.TargetQty is < 1))
        {
            throw new UnprocessableException(
                "assignee_quota_invalid", "ChềEtiêu của mỗi người nhận phải từ 1 trềElên.");
        }

        var explicitSum = assignments.Where(a => a.TargetQty.HasValue).Sum(a => a.TargetQty!.Value);
        if (explicitSum > targetQty)
        {
            throw new UnprocessableException(
                "assignment_quota_exceeds_target",
                $"Tổng quota {explicitSum} vượt quá chềEtiêu {targetQty} của task.");
        }

        var autoCount = assignments.Count(a => !a.TargetQty.HasValue);
        if (autoCount == 0)
        {
            if (explicitSum != targetQty)
            {
                throw new UnprocessableException(
                    "assignment_quota_not_equal_target",
                    $"Tổng quota đã đặt là {explicitSum}, phải bằng đúng chềEtiêu {targetQty} của task.");
            }

            return [.. assignments.Select(a => a.TargetQty!.Value)];
        }

        var remaining = targetQty - explicitSum;
        if (remaining < autoCount)
        {
            throw new UnprocessableException(
                "assignment_quota_not_enough",
                $"Còn {remaining} chềEtiêu cho {autoCount} người chưa đặt quota; mỗi người cần ít nhất 1.");
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
    /// trước đây lý do cụ thềEhơn là "task rơi vềEDraft, mà Draft không nhận việc
    /// nữa"  Etrạng thái `Draft` đã bềEbềEnên luật này giữ nguyên vì lý do thật của nó.
    /// </summary>
    public async Task<TaskDetailDto> UnassignAsync(
        long taskId, long userId, CancellationToken ct = default,
        long? callerId = null, bool isAdmin = false)
    {
        await using var transaction = await BeginTransactionIfNeededAsync(ct);
        var task = await LoadEditableAsync(taskId, ct);
        await EnsureWaveOwnerAsync(task, callerId, isAdmin, ct);

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

        // Chia lại phần của người vừa rời đi cho những người còn lại, đềEtổng quota
        // vẫn bằng chềEtiêu task và không ai bềEkẹt với mục tiêu cũ.
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
    /// nhận và quota mới. Mục đã có bản ghi, đã xong hoặc đang bềEgiữ trong hàng đợi
    /// giữ nguyên người nhận  Edữ liệu đã làm không đổi chủ.
    ///
    /// Một Speaker không bao giềEnhận hai mục của cùng một cặp câu; hai Speaker khác
    /// nhau nhận cùng cặp là hợp lềE
    /// </summary>
    private async Task RedistributeRecordingScriptsAsync(
        long taskId, IReadOnlyList<(long UserId, int Quota)> assignments, CancellationToken ct)
    {
        var items = await tasks.GetScriptItemsTrackedAsync(taskId, ct);
        if (items.Count == 0) return;

        var startedIds = await tasks.GetStartedTaskScriptIdsAsync(taskId, clock.GetUtcNow(), ct);

        var quotaByUser = assignments.ToDictionary(a => a.UserId, a => a.Quota);

        // Mục đã bắt đầu giữ nguyên người nhận và tính vào quota của hềE
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
        // Phần dư (và mục của người bềEgỡ) trôi vào danh sách tự do.
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

        // Không đặt mục vềEnull hàng loạt: hai dòng cùng câu sẽ đụng
        // uq_task_script_unassigned giữa chừng. ChềEgán thẳng cho người chưa giữ
        // câu đó  Emỗi lần ghi vẫn là một khoá (task, script, speaker) duy nhất.
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
        // chưa có cặp đó; không còn ai hợp lềEthì đềEtrống thay vì giao trùng.
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
        // đềEkhông đụng uq_task_script_unassigned.
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
    /// bằng chềEtiêu task; không dùng ceil vì ceil làm tổng vượt chềEtiêu.
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
    /// quota hiển thềEcho lượt giao cũ chưa có target_qty (dữ liệu trước V12).
    /// Lấy đúng phần của người ềEvềEtrí <paramref name="index"/> trong mảng đã sắp xếp
    /// (cùng thứ tự <see cref="SplitQuota"/> dùng khi chia), đềEtổng các phần hiển thềE
    /// đúng bằng chềEtiêu task. Min 1 cho dữ liệu cũ bất thường (target &lt; sềEngười),
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
    /// task duyệt trong đợt nhận việc qua đường tự đẩy lúc Speaker nộp, nên hàng chềE
    /// rỗng lúc giao KHÔNG phải lỗi  Egiao reviewer trước, bản ghi tự tới sau là luồng chính.
    /// Task thu âm và task duyệt ngoài đợt vẫn phải đủ mục trước khi giao/nâng chềEtiêu.
    /// </summary>
    private static bool RequiresItemsToAssign(WorkTask task) =>
        task.TaskType != TaskType.Review || task.WaveId is null;

    public async Task<TaskDetailDto> CancelAsync(
        long taskId, CancellationToken ct = default,
        long? callerId = null, bool isAdmin = false)
    {
        var task = await LoadEditableAsync(taskId, ct);
        await EnsureWaveOwnerAsync(task, callerId, isAdmin, ct);

        if (task.Status == WorkTaskStatus.Completed)
        {
            throw new ConflictException("task_completed", $"Task #{taskId} đã hoàn thành, không huỷ được.");
        }

        // đi qua helper chung với đường "Task Manager chọn Cancelled tay" ềE
        // UpdateAsync  Emột nơi ghi trạng thái, một nơi huỷ lượt giao.
        await ApplyManualStatusAsync(task, WorkTaskStatus.Cancelled, ct);

        // Các mục giữ nguyên đềEcòn dấu vết. Task đã huỷ không giữ chềEgì nữa:
        // truy vấn tự lấp chềEtránh những mục đang nằm trong task còn chạy.
        if (task.WaveId.HasValue) await waves.RefreshStatusAsync(task.WaveId.Value, ct);

        return await LoadDetailAsync(taskId, ct);
    }

    // Xoá cứng task đã gỡ khỏi đợt và chưa có dữ liệu nộp thật
    public async Task DeleteAsync(
        long taskId, CancellationToken ct = default,
        long? callerId = null, bool isAdmin = false)
    {
        await using var transaction = await BeginTransactionIfNeededAsync(ct);
        var task = await tasks.GetForUpdateAsync(taskId, ct) ?? throw NotFound(taskId);
        await EnsureWaveOwnerAsync(task, callerId, isAdmin, ct);

        // 1. Chặn xoá khi task vẫn đang nằm trong chiến dịch
        if (task.WaveId.HasValue)
        {
            var cName = (await waves.GetAsync(task.WaveId.Value, ct))?.WaveName;
            throw new ConflictException(
                "task_in_wave",
                $"Nhiệm vụ đang nằm trong đợt \"{cName ?? ("#" + task.WaveId)}\". Hãy gỡ khỏi đợt trước khi xoá.");
        }

        // 2. Chặn xoá khi task đã hoàn thành
        if (task.Status == WorkTaskStatus.Completed)
        {
            throw new ConflictException("task_completed", $"Nhiệm vụ #{taskId} đã hoàn thành, không xoá được.");
        }

        // 3. Chặn xoá khi task đã có dữ liệu nộp/duyệt thật (bản ghi nộp vềE lượt duyệt).
        //    Lượt giao và hàng chềEchềElà sềEsách  Etask_assignment / task_script /
        //    task_recording xoá theo task (ON DELETE CASCADE).
        var hasSubmitted = await tasks.HasSubmittedWorkAsync(taskId, ct);
        if (hasSubmitted)
        {
            throw new ConflictException(
                "task_has_work",
                $"Nhiệm vụ #{taskId} đã có dữ liệu nộp/duyệt. Dùng chức năng Huỷ nếu không làm nữa đềEgiữ dấu vết.");
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
            // của người này trong phép chia (cùng thứ tự Assignees đã sắp xếp), đềEtổng
            // hiển thềEđúng bằng chềEtiêu task.
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

        var progress = await waves.GetProgressAsync(null, ct);

        var mine = progress
            .Where(p => isAdmin || p.AssignedTo == managerId)
            .Select(p => new WaveProgressDto(
                p.WaveId, p.WaveName, p.Domain, p.WaveTargetQty, p.StartDate, p.EndDate,
                p.WaveStatus, p.AssignedTo, p.AllocatedTaskQty, p.RemainingTaskQty,
                p.TaskCount, p.CompletedTaskCount))
            .ToList();

        var byAssignee = await GetAssigneeSummaryAsync(ct);

        // Đếm task còn chạy theo loại + quá hạn: chềEcần Total nên pageSize = 1.
        // ChềEđếm nên thứ tự không quan trọng  ESortSpec rỗng là thứ tự mặc định.
        // chềEcòn một trạng thái "đang chạy" là InProgress, nên không cộng
        // `Open` với `InProgress` nữa.
        var (_, activeRec) = await tasks.SearchAsync(
            TaskType.Recording, WorkTaskStatus.InProgress, null, null, null, now, 1, 1, default, null, false, ct);
        var (_, activeRev) = await tasks.SearchAsync(
            TaskType.Review, WorkTaskStatus.InProgress, null, null, null, now, 1, 1, default, null, false, ct);
        var (_, overdue) = await tasks.SearchAsync(
            null, null, null, null, true, now, 1, 1, default, null, false, ct);

        return new TaskManagerOverviewDto(mine, byAssignee, activeRec, activeRev, overdue);
    }

    // ----------------------------------------------------------------- nội bềE

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
                skipped.Add(new SkippedItemDto(id, $"Đang ềEtrạng thái {script.Status}, chềEnhận câu đã duyệt nội dung."));
            else if (script.Domain != taskDomain)
                skipped.Add(new SkippedItemDto(id, $"Chủ đềEcâu ({script.Domain}) không khớp với chủ đềEnhiệm vụ ({taskDomain})."));
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
                skipped.Add(new SkippedItemDto(id, $"Đang ềEtrạng thái {recording.Status}, không còn chềEduyệt."));
            else if (recording.Reviews.Count >= roundsRequired)
                skipped.Add(new SkippedItemDto(id, $"Đã đủ {roundsRequired} lượt duyệt."));
            // bản ghi của bất kỳ ai trong nhóm nhận task đều không thềEđưa
            // vào task này  Engười duyệt không được duyệt bản ghi của chính mình, và
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
        // cùng nghĩa với nhánh `Draft` cũ  Echưa giao cho ai thì chưa có mục nào
        // đềEkiểm tra, cứ đềETask Manager sửa tiếp.
        var active = await tasks.GetOpenAssignmentsAsync(task.TaskId, ct);
        if (active.Count == 0) return;

        var (workable, blocked) = await CountWorkableAsync(task, active, itemId, ct);

        // task duyệt trong đợt tự đầy lại khi có bản ghi mới, nên gỡ một mục
        // không làm task kẹt  Ecùng lý do với RequiresItemsToAssign ềEđường giao việc.
        if (RequiresItemsToAssign(task) && task.TargetQty > workable)
        {
            throw new ConflictException(
                "target_exceeds_items",
                $"Gỡ mục {itemId} thì task không còn đủ mục. " +
                DescribeShortfall(task.TargetQty, workable, blocked) + " Hạ chềEtiêu trước.");
        }
    }

    /// <summary>
    /// SềEmục nhóm người nhận còn làm được: bềEmục đã loại, và bềEmục mà KHÔNG AI
    /// trong nhóm làm được  Ecặp câu của người đọc khác, hoặc bản ghi do chính người
    /// nhận task thu (người duyệt không được duyệt bản của mình).
    ///
    /// trước đây đếm theo MỘT người nhận. Nhiều người thì phải hỏi "cả nhóm
    /// còn làm được bao nhiêu", không phải "người này còn làm được bao nhiêu"  Enếu
    /// tính theo một người thì có thềEbáo thiếu mục trong khi người khác vẫn làm được.
    ///
    /// <paramref name="Blocked"/> trả vềEmọi mục bềEít nhất một người chặn, đềEthông báo
    /// lỗi nói rõ vấn đềEnằm ềEđâu; sềEtrừ vào chềEtiêu là <c>BlockedForEveryone</c>.
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

        // Giao các danh sách chặn: mục nào xuất hiện ềEMỌI người thì mới thật sự
        // không ai làm được. ChềEmột người chặn thì người khác vẫn nhận phần việc đó.
        var blockedForEveryone = perUser[0].Count(id => perUser.All(set => set.Contains(id)));

        return (Math.Max(usable - blockedForEveryone, 0), blocked);
    }

    private static string DescribeShortfall(int target, int workable, List<string> blocked)
    {
        var message = $"ChềEtiêu {target} vượt quá {Math.Max(workable, 0)} mục người nhận làm được.";

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
            throw new ConflictException("task_cancelled", $"Task #{taskId} đã bềEhuỷ, không sửa được nữa.");
        }

        return task;
    }

    /// <summary>
    /// Task đã Completed thì không giao/thêm/gỡ mục nữa  Emuốn làm tiếp thì Task Manager
    /// nâng chềEtiêu qua PATCH đềEtask tự mềElại. Update và Cancel dùng LoadEditableAsync riêng.
    /// </summary>
    private async Task<WorkTask> LoadMutableAsync(long taskId, CancellationToken ct)
    {
        var task = await LoadEditableAsync(taskId, ct);

        if (task.Status == WorkTaskStatus.Completed)
        {
            throw new ConflictException(
                "task_completed",
                $"Task #{taskId} đã hoàn thành. Muốn thêm việc thì nâng chềEtiêu trước, task sẽ tự mềElại.");
        }

        return task;
    }

    /// <summary>
    /// ChềETask Manager sềEhữu chiến dịch (wave.AssignedTo) hoặc Admin được sửa task.
    /// Task độc lập (wave_id NULL): chềEngười tạo hoặc Admin được sửa.
    /// callerId null nghĩa là caller cũ (test)  EbềEqua đềEtương thích ngược.
    /// </summary>
    private async Task EnsureWaveOwnerAsync(WorkTask task, long? callerId, bool isAdmin, CancellationToken ct)
    {
        if (!callerId.HasValue) return;
        if (isAdmin) return;

        if (!task.WaveId.HasValue)
        {
            if (task.CreatedBy != callerId.Value)
            {
                throw new ForbiddenException(
                    "task_not_owned",
                    $"Task #{task.TaskId} là task độc lập do người khác tạo nên bạn không sửa được.");
            }
            return;
        }

        var wave = await waves.GetAsync(task.WaveId.Value, ct)
            ?? throw new NotFoundException("wave_not_found", $"Không tìm thấy chiến dịch #{task.WaveId}.");

        if (wave.AssignedTo != callerId.Value)
        {
            throw new ForbiddenException(
                "task_not_owned",
                $"Task #{task.TaskId} thuộc chiến dịch #{wave.WaveId} không giao cho bạn.");
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
            throw new UnprocessableException("user_inactive", $"Tài khoản {user.Email} đang bềEvô hiệu hoá.");
        }

        if (user.Role.RoleName != requiredRole)
        {
            throw new UnprocessableException(
                "assignee_role_mismatch",
                $"Task {taskType} chềEgiao được cho {requiredRole}, còn {user.Email} là {user.Role.RoleName}.");
        }

        return user;
    }

    /// <summary>
    /// MềEtransaction riêng, trừ khi caller đã ềEtrong một transaction (test
    /// tích hợp giữ transaction ambient đềErollback). EF không cho transaction
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
    /// kiểm tra ềEtrên) thành lỗi nghiệp vụ có mã đềEfrontend hiện được.
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
                "task_target_exceeds_wave",
                "ChềEtiêu task vượt phần còn lại của chiến dịch. Lấy allocatedTaskQty ềEdanh sách chiến dịch đềEchia lại.");
        }

        if (message.Contains("must be within wave", StringComparison.OrdinalIgnoreCase))
        {
            return new UnprocessableException(
                "task_deadline_outside_wave",
                "Hạn của task nằm ngoài khoảng ngày của chiến dịch. Giới hạn ô chọn ngày theo startDate/endDate.");
        }

        if (message.Contains("no assigned task manager", StringComparison.OrdinalIgnoreCase)
            || message.Contains("must be the assigned task manager", StringComparison.OrdinalIgnoreCase))
        {
            return new UnprocessableException(
                "wave_not_assigned_to_manager",
                "Chiến dịch chưa được giao cho bạn nên chưa nhận task. NhềEAdmin giao chiến dịch trước.");
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
    /// Kiểu timestamptz qua Npgsql CHềEnhận DateTimeOffset có offset 0, nên phải đổi sang UTC.
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
            $"Mục {itemId} đã ềEtrạng thái {status}, không gỡ được  Egỡ đi là mất dấu công việc đã làm.");
}
