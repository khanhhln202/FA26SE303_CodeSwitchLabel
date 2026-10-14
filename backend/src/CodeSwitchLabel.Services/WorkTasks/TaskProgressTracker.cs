using CodeSwitchLabel.Repositories.Entities;
using CodeSwitchLabel.Repositories.Enums;
using CodeSwitchLabel.Repositories.Repositories;

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
    Task OnRecordingSubmittedAsync(long taskId, CancellationToken ct = default);

    /// <summary>
    /// Gọi SAU khi đã ghi lượt duyệt và đọc lại trạng thái bản ghi do trigger chốt.
    /// </summary>
    /// <param name="finalStatus">Trạng thái bản ghi hiện tại: còn chờ, hoặc đã chốt.</param>
    Task OnReviewSubmittedAsync(
        Recording recording, RecordingStatus finalStatus, long? taskId, CancellationToken ct = default);

    /// <summary>Cặp câu bị từ chối nội dung thì mọi task đang chứa nó đều mất mục đó.</summary>
    Task OnScriptRejectedAsync(string scriptId, CancellationToken ct = default);

    /// <summary>
    /// TEAM_010: bản ghi bị loại nhưng câu giữ lại (nút "Chỉ xóa audio") — người đọc
    /// sẽ thu lại, nên mục câu phải quay về hàng chờ và chỉ tiêu phải bị trừ.
    /// </summary>
    Task OnRecordingReturnedAsync(Recording recording, long? taskId, CancellationToken ct = default);

    /// <summary>
    /// TEAM_010: xoá cả cặp (nút "Xóa hoàn toàn") — câu không còn là mục công việc của
    /// task nào nữa, và mọi task chứa nó phải tính lại chỉ tiêu.
    /// </summary>
    Task OnPairDiscardedAsync(string scriptId, CancellationToken ct = default);

    Task RefreshStatusAsync(long taskId, CancellationToken ct = default);
}

public class TaskProgressTracker(ITaskRepository tasks) : ITaskProgressTracker
{
    public Task OnRecordingSubmittedAsync(long taskId, CancellationToken ct = default) =>
        RefreshStatusAsync(taskId, ct);

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

            // Một cặp câu chỉ tính là xong khi CẢ HAI bản cs và vi đều được duyệt đạt.
            if (finalStatus == RecordingStatus.Approved &&
                await tasks.IsScriptFullyApprovedAsync(recording.ScriptId, ct))
            {
                foreach (var item in await tasks.GetPendingScriptItemsAsync(recording.ScriptId, ct))
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
                foreach (var item in await tasks.GetCompletedScriptItemsAsync(recording.ScriptId, ct))
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
        // chờ vì kết quả cũ đã bị gỡ. Không sửa thì task vẫn tính câu này là xong.
        foreach (var item in await tasks.GetCompletedScriptItemsAsync(recording.ScriptId, ct))
        {
            item.Status = TaskScriptStatus.Pending;
            touched.Add(item.TaskId);
        }

        await tasks.SaveChangesAsync(ct);

        foreach (var id in touched) await RefreshStatusAsync(id, ct);
    }

    public async Task OnPairDiscardedAsync(string scriptId, CancellationToken ct = default)
    {
        var pending = await tasks.GetPendingScriptItemsAsync(scriptId, ct);
        var completed = await tasks.GetCompletedScriptItemsAsync(scriptId, ct);

        if (pending.Count == 0 && completed.Count == 0) return;

        // Gom taskId TRƯỚC khi sửa: sau khi lưu thì truy vấn lại sẽ trả về rỗng.
        var taskIds = pending.Concat(completed).Select(i => i.TaskId).Distinct().ToList();

        foreach (var item in pending) item.Status = TaskScriptStatus.Rejected;
        foreach (var item in completed) item.Status = TaskScriptStatus.Rejected;

        await tasks.SaveChangesAsync(ct);

        foreach (var id in taskIds) await RefreshStatusAsync(id, ct);
    }

    public async Task RefreshStatusAsync(long taskId, CancellationToken ct = default)
    {
        var task = await tasks.GetForUpdateAsync(taskId, ct);
        var row = await tasks.GetRowAsync(taskId, ct);

        if (task is null || row is null) return;

        var next = TaskStateMachine.Evaluate(
            task.Status, row.AssigneeCount, row.Started, row.Done, task.TargetQty);

        if (next == task.Status) return;

        task.Status = next;
        await tasks.SaveChangesAsync(ct);
    }
}
