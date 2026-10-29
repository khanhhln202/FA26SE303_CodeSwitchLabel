using CodeSwitchLabel.Repositories.Entities;
using CodeSwitchLabel.Repositories.Enums;
using CodeSwitchLabel.Repositories.Repositories;
using CodeSwitchLabel.Services.Implementations;

namespace CodeSwitchLabel.Services.WorkTasks;

/// <summary>
/// Cầu nối giữa công việc thật — nộp bản ghi, duyệt, từ chối nội dung — và tiến độ task.
/// Module Recording, Review và Script gọi vào đây; module Task không phải biết họ tồn tại.
///
/// Mọi thay đổi đi qua cùng DbContext với nơi gọi, nên khi nơi gọi đang ở trong transaction
/// thì tiến độ task nằm luôn trong transaction đó.
/// </summary>
public interface ITaskProgressTracker
{
    /// <summary>
    /// Speaker nộp bản ghi thì bản chờ duyệt tự vào hàng chờ của MỌI task duyệt
    /// đang mở trong cùng đợt — Reviewer nhận việc trước vẫn có việc ngay, Task Manager
    /// không phải "Thêm mục" thủ công lần nữa. Sau đó vẫn tính lại trạng thái task thu âm.
    /// </summary>
    Task OnRecordingSubmittedAsync(Recording recording, CancellationToken ct = default);

    /// <summary>
    /// Gọi SAU khi đã ghi lượt duyệt và đọc lại trạng thái bản ghi do trigger chốt.
    /// </summary>
    /// <param name="finalStatus">Trạng thái bản ghi hiện tại: còn chờ, hoặc đã chốt.</param>
    Task OnReviewSubmittedAsync(
        Recording recording, RecordingStatus finalStatus, long? taskId, CancellationToken ct = default);

    /// <summary>Cặp câu bị từ chối nội dung thì mọi task đang chứa nó đều mất mục đó.</summary>
    Task OnScriptRejectedAsync(string scriptId, CancellationToken ct = default);

    /// <summary>
    /// bản ghi bị loại nhưng câu giữ lại (nút "Chỉ xóa audio") — người đọc
    /// sẽ thu lại, nên mục câu phải quay về hàng chờ và chỉ tiêu phải bị trừ.
    /// </summary>
    Task OnRecordingReturnedAsync(Recording recording, long? taskId, CancellationToken ct = default);

    /// <summary>
    /// xoá cả cặp (nút "Xóa hoàn toàn") — câu không còn là mục công việc của
    /// task nào nữa, và mọi task chứa nó phải tính lại chỉ tiêu.
    /// </summary>
    Task OnPairDiscardedAsync(string scriptId, CancellationToken ct = default);

    Task RefreshStatusAsync(long taskId, CancellationToken ct = default);
}

public class TaskProgressTracker(
    ITaskRepository tasks, ICampaignRepository campaigns, TimeProvider clock)
    : ITaskProgressTracker
{
    public async Task OnRecordingSubmittedAsync(Recording recording, CancellationToken ct = default)
    {
        if (recording.TaskId is not { } taskId) return;

        if (recording.Status == RecordingStatus.PendingReview)
        {
            await PushToCampaignReviewTasksAsync(recording, taskId, ct);
        }

        await RefreshStatusAsync(taskId, ct);
    }

    /// <summary>
    /// thêm dòng task_recording (Queued) cho bản ghi vào mọi task duyệt chưa huỷ
    /// của đợt — gồm cả task đã đủ chỉ tiêu, theo người nhận vẫn duyệt thêm được.
    /// Bản trượt QC không tới vòng duyệt; bản thu ngoài đợt thì Reviewer nhặt ở hàng chờ chung.
    /// </summary>
    private async Task PushToCampaignReviewTasksAsync(
        Recording recording, long taskId, CancellationToken ct)
    {
        var campaignId = await tasks.GetCampaignIdAsync(taskId, ct);
        if (campaignId is null) return;

        var reviewTaskIds = await tasks.GetReviewTaskIdsForCampaignAsync(campaignId.Value, ct);
        if (reviewTaskIds.Count == 0) return;

        // Bản ghi vừa được tạo nên chưa thể nằm trong task nào — không cần lọc trùng.
        // Khoá chính (task_id, recording_id) của bảng vẫn là lưới an toàn cuối cùng.
        var now = clock.GetUtcNow();
        tasks.AddRecordingItems(reviewTaskIds.Select(id => new TaskRecording
        {
            TaskId = id,
            RecordingId = recording.RecordingId,
            Status = TaskRecordingStatus.Queued,
            IncludedAt = now
        }));

        await tasks.SaveChangesAsync(ct);
    }

    public async Task OnReviewSubmittedAsync(
        Recording recording, RecordingStatus finalStatus, long? taskId, CancellationToken ct = default)
    {
        var touched = new HashSet<long>();
        if (taskId.HasValue) touched.Add(taskId.Value);

        if (finalStatus != RecordingStatus.PendingReview)
        {
            // Bản ghi đã chốt: task khác còn giữ nó trong hàng chờ thì không duyệt được nữa.
            // Không gỡ thì mục đó kẹt vĩnh viễn và task không bao giờ đạt chỉ tiêu.
            foreach (var item in await tasks.GetQueuedRecordingItemsAsync(recording.RecordingId, ct))
            {
                item.Status = TaskRecordingStatus.Skipped;
                touched.Add(item.TaskId);
            }

            // Một cặp câu chỉ tính là xong cho MỘT Speaker khi cả hai bản cs và vi
            // của chính người đó đều được duyệt đạt. Speaker khác có bản riêng.
            if (finalStatus == RecordingStatus.Approved &&
                await tasks.IsScriptFullyApprovedForSpeakerAsync(
                    recording.ScriptId, recording.SpeakerId, ct))
            {
                foreach (var item in await tasks.GetPendingScriptItemsForSpeakerAsync(
                             recording.ScriptId, recording.SpeakerId, ct))
                {
                    item.Status = TaskScriptStatus.Completed;
                    touched.Add(item.TaskId);
                }
            }

            // TEAM_010 (yêu cầu 4.2) — TRỪ KPI khi bản ghi bị từ chối.
            //
            // Trước đây task_script chỉ đi được Pending → Completed, không có đường lùi.
            // Nên câu đã tính xong rồi bị từ chối vẫn giữ nguyên Done, task vẫn đứng ở
            // Completed dù chỉ tiêu thực tế đã tụt. Người nhận không bao giờ được biết
            // là phải thu bù.
            if (finalStatus == RecordingStatus.Rejected)
            {
                foreach (var item in await tasks.GetCompletedScriptItemsForSpeakerAsync(
                             recording.ScriptId, recording.SpeakerId, ct))
                {
                    item.Status = TaskScriptStatus.Pending;
                    touched.Add(item.TaskId);
                }

                // Mục trong task DUYỆT cũng phải rút lại: bản ghi đã bị loại thì lượt
                // duyệt của nó không còn đóng góp cho chỉ tiêu nữa. Chuyển sang Skipped
                // chứ không phải Queued — bản ghi đã chốt rồi, không ai duyệt lại được.
                foreach (var item in await tasks.GetReviewedRecordingItemsAsync(recording.RecordingId, ct))
                {
                    item.Status = TaskRecordingStatus.Skipped;
                    touched.Add(item.TaskId);
                }
            }
        }

        await tasks.SaveChangesAsync(ct);

        foreach (var id in touched)
        {
            await RefreshStatusAsync(id, ct);
        }
    }

    public async Task OnScriptRejectedAsync(string scriptId, CancellationToken ct = default)
    {
        var items = await tasks.GetPendingScriptItemsAsync(scriptId, ct);
        if (items.Count == 0) return;

        foreach (var item in items)
        {
            item.Status = TaskScriptStatus.Rejected;
        }

        await tasks.SaveChangesAsync(ct);

        foreach (var taskId in items.Select(i => i.TaskId).Distinct())
        {
            await RefreshStatusAsync(taskId, ct);
        }
    }

    public async Task OnRecordingReturnedAsync(Recording recording, long? taskId, CancellationToken ct = default)
    {
        var touched = new HashSet<long>();
        if (taskId.HasValue) touched.Add(taskId.Value);

        // Cặp câu còn dùng được nên mục vẫn giữ trong task — nhưng phải về lại hàng
        // chờ vì kết quả cũ đã bị gỡ. Chỉ mục của chính Speaker này bị ảnh hưởng.
        foreach (var item in await tasks.GetCompletedScriptItemsForSpeakerAsync(
                     recording.ScriptId, recording.SpeakerId, ct))
        {
            item.Status = TaskScriptStatus.Pending;
            touched.Add(item.TaskId);
        }

        // bản ghi bị gỡ khỏi vòng duyệt thì mọi task duyệt còn giữ nó trong hàng
        // chờ phải bỏ mục. Thiếu bước này, các task duyệt anh em — nơi bản ghi tự vào lúc
        // Speaker nộp — giữ mục `queued` trỏ vào bản đã bị loại: mục kẹt vĩnh viễn và số mục
        // của task bị đếm sai.
        foreach (var item in await tasks.GetQueuedRecordingItemsAsync(recording.RecordingId, ct))
        {
            item.Status = TaskRecordingStatus.Skipped;
            touched.Add(item.TaskId);
        }

        await tasks.SaveChangesAsync(ct);

        foreach (var id in touched) await RefreshStatusAsync(id, ct);
    }

    public async Task OnPairDiscardedAsync(string scriptId, CancellationToken ct = default)
    {
        var pending = await tasks.GetPendingScriptItemsAsync(scriptId, ct);
        var completed = await tasks.GetCompletedScriptItemsAsync(scriptId, ct);

        // xoá cả cặp thì MỌI bản ghi của câu bị loại, nên mục đang chờ ở các task
        // duyệt anh em — nơi bản ghi tự vào lúc Speaker nộp — cũng chết theo. Gác sau khi
        // lấy đủ ba nhóm: câu có thể chỉ nằm trong task duyệt (thêm tay) mà không có mục câu nào.
        var queuedReviews = await tasks.GetQueuedRecordingItemsByScriptAsync(scriptId, ct);

        if (pending.Count == 0 && completed.Count == 0 && queuedReviews.Count == 0) return;

        // Gom taskId TRƯỚC khi sửa: sau khi lưu thì truy vấn lại sẽ trả về rỗng.
        var taskIds = pending.Concat(completed).Select(i => i.TaskId)
            .Concat(queuedReviews.Select(i => i.TaskId))
            .Distinct().ToList();

        foreach (var item in pending) item.Status = TaskScriptStatus.Rejected;
        foreach (var item in completed) item.Status = TaskScriptStatus.Rejected;
        foreach (var item in queuedReviews) item.Status = TaskRecordingStatus.Skipped;

        await tasks.SaveChangesAsync(ct);

        foreach (var id in taskIds) await RefreshStatusAsync(id, ct);
    }

    public async Task RefreshStatusAsync(long taskId, CancellationToken ct = default)
    {
        var task = await tasks.GetForUpdateAsync(taskId, ct);
        var row = await tasks.GetRowAsync(taskId, ct);

        if (task is null || row is null) return;

        var next = TaskStateMachine.Evaluate(task.Status, row.Done, task.TargetQty);
        var taskStatusChanged = next != task.Status;

        if (taskStatusChanged) task.Status = next;

        // V17: trạng thái lượt giao cũng là HÀM của số liệu thật — đạt quota riêng thì
        // Completed, tụt xuống dưới quota (bản ghi bị từ chối, chỉ tiêu tăng) thì Active.
        var assignmentsChanged = await SyncAssignmentStatusesAsync(task, row, ct);

        if (!taskStatusChanged && !assignmentsChanged)
        {
            // status không đổi thì đợt cũng không đổi theo — bỏ qua cho khỏi
            // khoá dòng campaign ở mọi lần nộp bản ghi/duyệt (hàng chục lần một task).
            return;
        }

        await tasks.SaveChangesAsync(ct);

        // trạng thái đợt là hàm của task, nên task đổi trạng thái thì đợt tính lại.
        if (taskStatusChanged && task.CampaignId.HasValue)
            await campaigns.RefreshStatusAsync(task.CampaignId.Value, ct);
    }

    /// <summary>
    /// V17 — quota riêng của từng người nhận. Mỗi lượt giao đang mở (Active/Completed)
    /// được chấm lại theo đúng số việc người đó đã làm xong, không cộng dồn từng bước:
    ///   done &gt;= quota → Completed
    ///   done &lt;  quota → Active
    ///
    /// Completed KHÔNG phải trạng thái cuối và không khoá công việc (TEAM_016): người
    /// nhận vẫn thu/duyệt thêm được. Nó chỉ nói "đã đạt phần của mình"; nếu task nâng
    /// quota hoặc một bản ghi bị từ chối làm done tụt, lượt giao tự mở lại Active.
    ///
    /// Lượt giao Cancelled/Reassigned không bị đụng — người đó đã rời task.
    /// </summary>
    private async Task<bool> SyncAssignmentStatusesAsync(
        WorkTask task, TaskRow row, CancellationToken ct)
    {
        // Task đã huỷ thì không mở lại lượt giao nào; CancelAsync đã chuyển các lượt
        // đang chờ sang Cancelled.
        if (task.Status == WorkTaskStatus.Cancelled) return false;

        var open = await tasks.GetOpenAssignmentsAsync(task.TaskId, ct);
        if (open.Count == 0) return false;

        var done = await tasks.GetAssigneeDoneAsync(task.TaskId, task.TaskType, ct);
        var byUser = open.ToDictionary(a => a.UserId);
        var changed = false;

        // row.Assignees sắp theo tên và là nguồn quota hiển thị cho người nhận; dùng
        // đúng thứ tự đó để lượt giao cũ chưa có target_qty chia khớp với myProgress.
        for (var i = 0; i < row.Assignees.Count; i++)
        {
            var assignee = row.Assignees[i];
            if (!byUser.TryGetValue(assignee.UserId, out var assignment)) continue;

            var quota = assignee.TargetQty ?? TaskService.SplitQuotaAt(
                task.TargetQty, row.Assignees.Count, i);

            var desired = quota > 0 && done.GetValueOrDefault(assignee.UserId) >= quota
                ? AssignmentStatus.Completed
                : AssignmentStatus.Active;

            if (assignment.AssignmentStatus == desired) continue;

            assignment.AssignmentStatus = desired;
            changed = true;
        }

        return changed;
    }
}
