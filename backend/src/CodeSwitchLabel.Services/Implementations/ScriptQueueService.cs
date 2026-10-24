using CodeSwitchLabel.Repositories.Entities;
using CodeSwitchLabel.Repositories.Enums;
using CodeSwitchLabel.Repositories.Repositories;
using CodeSwitchLabel.Services.Abstractions;
using CodeSwitchLabel.Services.Common;
using CodeSwitchLabel.Services.Dtos;

namespace CodeSwitchLabel.Services.Implementations;

public class ScriptQueueService(
    IScriptQueueRepository queue,
    IScriptRepository scripts,
    ITaskRepository tasks,
    ISystemConfigService config) : IScriptQueueService
{
    public async Task<ScriptDetailDto?> ClaimNextForTextReviewAsync(
        long reviewerId, long? taskId, CancellationToken ct = default)
    {
        if (taskId.HasValue && !await tasks.IsActiveTaskOfAsync(taskId.Value, reviewerId, TaskType.Review, ct))
        {
            throw new ForbiddenException(
                "task_not_reviewable",
                $"Task #{taskId.Value} không phải task duyệt đang giao cho bạn.");
        }

        return await ClaimAsync(reviewerId, QueueStage.TextReview, taskId, ct);
    }

    public async Task<ScriptDetailDto?> ClaimNextForRecordingAsync(
        long speakerId, long? taskId, CancellationToken ct = default)
    {
        if (taskId.HasValue && !await tasks.IsActiveTaskOfAsync(taskId.Value, speakerId, TaskType.Recording, ct))
        {
            throw new ForbiddenException(
                "task_not_recordable", $"Task #{taskId.Value} không phải task thu âm đang giao cho bạn.");
        }

        return await ClaimAsync(speakerId, QueueStage.Recording, taskId, ct);
    }

    private async Task<ScriptDetailDto?> ClaimAsync(
        long userId, QueueStage stage, long? taskId, CancellationToken ct)
    {
        var claim = await queue.ClaimNextAsync(userId, stage, taskId, ct);

        if (claim is null) return null;

        var script = await scripts.GetWithReviewsAsync(claim.ScriptId, ct);

        // Hàng đợi và bảng script lệch nhau thì nghĩa là dữ liệu bị sửa tay bên ngoài.
        // Trả null an toàn hơn là trả một câu không tồn tại cho client.
        return script is null ? null : script.ToDetail();
    }

    public async Task<bool> CanReviewAsync(string scriptId, long userId, CancellationToken ct = default)
    {
        var row = await queue.GetOpenRowAsync(scriptId, QueueStage.TextReview, ct);

        if (row is null) return false;

        // Chưa gán lô nào thì ai cũng lấy được — đó là hàng đợi chung.
        if (row.TaskId is null) return true;

        return await tasks.IsActiveTaskOfAsync(row.TaskId.Value, userId, TaskType.Review, ct);
    }

    public async Task<PagedResult<ContributionReviewItemDto>> ListPendingContributionsAsync(
        long reviewerId, ContributionSearchRequest request, CancellationToken ct = default)
    {
        var (rows, total) = await queue.ListPendingContributionsAsync(
            request.Domain, request.Page, request.PageSize, ct);

        // Câu chưa chia lô thì ai cũng duyệt được; câu đã chia thì chỉ người nhận lô đó.
        // Hỏi một lần cho mỗi lô thay vì mỗi dòng: một trang thường chỉ chạm một hai lô.
        var cuaToi = new Dictionary<long, bool>();

        foreach (var taskId in rows.Where(r => r.TaskId is not null).Select(r => r.TaskId!.Value).Distinct())
        {
            cuaToi[taskId] = await tasks.IsActiveTaskOfAsync(taskId, reviewerId, TaskType.Review, ct);
        }

        var items = rows
            .Select(r => new ContributionReviewItemDto(
                r.ScriptId, r.CsContent, r.ViContent, r.Domain, r.CreatedAt,
                new ContributorDto(r.ContributorId, r.ContributorName),
                r.TaskId is null || cuaToi[r.TaskId.Value]
                    ? ContributionBlocker.None
                    : ContributionBlocker.AssignedToOtherTask))
            .ToList();

        // Lọc sau khi tính chặn: phân trang vẫn chạy trên toàn bộ câu chờ duyệt, nên tổng số
        // không đổi theo người xem. Trang có thể ngắn hơn pageSize — đó là chủ ý.
        if (request.OnlyReviewable)
        {
            items = [.. items.Where(i => i.CanReview)];
        }

        return new PagedResult<ContributionReviewItemDto>(items, request.Page, request.PageSize, total);
    }

    public async Task<AssignTextBatchResult> ClaimTextBatchAsync(
        long taskId, long userId, int? count, CancellationToken ct = default)
    {
        var size = count ?? await config.GetIntAsync(ConfigKeys.TextReviewBatchSize, 100, ct);

        if (size < 1)
        {
            throw new UnprocessableException(
                "batch_size_invalid", "Số câu chia lô phải từ 1 trở lên.");
        }

        var task = await tasks.GetForUpdateAsync(taskId, ct)
                   ?? throw new NotFoundException("task_not_found", $"Không tìm thấy task #{taskId}.");

        if (task.TaskType != TaskType.Review)
        {
            throw new UnprocessableException(
                "not_a_review_task",
                $"Task #{taskId} là task {task.TaskType}. Chỉ task duyệt mới chia được lô câu văn bản.");
        }

        var already = (await tasks.GetOpenAssignmentsAsync(taskId, ct)).Any(a => a.UserId == userId);

        if (!already)
        {
            throw new ForbiddenException(
                "assignee_required",
                $"Người dùng #{userId} không nhận task #{taskId}. Giao lượt giao trước rồi hãy chia lô.");
        }

        var claimed = await queue.ClaimBatchAsync(taskId, userId, size, QueueStage.TextReview, ct);

        return new AssignTextBatchResult(taskId, size, claimed.Count, claimed);
    }

    public Task CompleteAsync(string scriptId, QueueStage stage, CancellationToken ct = default) =>
        queue.ReleaseAsync(scriptId, stage, QueueState.Done, ct: ct);

    /// <summary>
    /// Bỏ qua câu vừa lấy được nhưng không dùng được, và đẩy nó xuống CUỐI hàng đợi.
    ///
    /// Phải đẩy xuống cuối chứ không giữ chỗ cũ: hàng đợi xếp theo enqueued_at, nên
    /// trả về chỗ cũ là câu đó lại đứng đầu và bị claim lần sau — vòng lặp vô tận.
    /// </summary>
    public Task ReleaseAsync(string scriptId, QueueStage stage, CancellationToken ct = default) =>
        queue.ReleaseAsync(scriptId, stage, QueueState.Queued, deferToBack: true, ct);
    public Task RequeueAsync(string scriptId, QueueStage stage, CancellationToken ct = default) =>
        queue.RequeueAsync(scriptId, stage, ct);

    public Task<QueueStatsDto> GetStatsAsync(QueueStage stage, CancellationToken ct = default) =>
        queue.GetStatsAsync(stage, ct);
}
