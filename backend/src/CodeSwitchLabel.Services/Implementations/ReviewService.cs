using CodeSwitchLabel.Repositories.Entities;
using CodeSwitchLabel.Repositories.Enums;
using CodeSwitchLabel.Repositories.Persistence;
using CodeSwitchLabel.Repositories.Repositories;
using CodeSwitchLabel.Repositories.Storage;
using CodeSwitchLabel.Services.Abstractions;
using CodeSwitchLabel.Services.Common;
using CodeSwitchLabel.Services.Dtos;
using CodeSwitchLabel.Services.Reviews;
using CodeSwitchLabel.Services.WorkTasks;
using Microsoft.Extensions.Logging;

namespace CodeSwitchLabel.Services.Implementations;

internal static class ReviewMapper
{
    public static ReviewDto ToDto(this Review v, bool includeReviewer) =>
        new(v.ReviewId,
            v.ReviewRound,
            includeReviewer ? v.ReviewerId : null,
            v.Decision,
            v.IsBlind,
            v.Comment,
            [.. v.RejectionReasons.Select(rr =>
                new ReviewReasonDto(rr.Reason.ReasonCode, rr.Reason.Category, rr.Reason.Description))],
            v.ReviewedAt);
}

public class ReviewService(
    IReviewRepository reviews,
    IRecordingRepository recordings,
    IObjectStorage storage,
    ISystemConfigService config,
    ITaskProgressTracker taskTracker,
    TimeProvider clock,
    ILogger<ReviewService> logger) : IReviewService
{
    public async Task<NextReviewDto?> GetNextAsync(
        long reviewerId, long? taskId, long? speakerId, bool random, CancellationToken ct = default)
    {
        if (taskId.HasValue && !await reviews.IsActiveReviewTaskOfAsync(taskId.Value, reviewerId, ct))
        {
            throw new ForbiddenException(
                "task_not_reviewable", $"Task #{taskId} không phải task duyệt đang giao cho bạn.");
        }

        var roundsRequired = await RoundsRequiredAsync(ct);

        var recording = await reviews.GetNextForReviewerAsync(
            reviewerId, taskId, speakerId, random, roundsRequired, ct);

        if (recording is null) return null;

        var existing = await reviews.GetReviewsAsync(recording.RecordingId, ct);

        return await BuildPayloadAsync(recording, existing.Count, roundsRequired, ct);
    }

    public async Task<PagedResult<TaskReviewItemDto>> GetTaskRecordingsAsync(
        long taskId, long reviewerId, TaskReviewQuery query, CancellationToken ct = default)
    {
        if (!await reviews.IsActiveReviewTaskOfAsync(taskId, reviewerId, ct))
        {
            throw new ForbiddenException(
                "task_not_reviewable", $"Task #{taskId} không phải task duyệt đang giao cho bạn.");
        }

        var roundsRequired = await RoundsRequiredAsync(ct);

        var (rows, total) = await reviews.GetTaskReviewRowsAsync(
            taskId, reviewerId, query.OnlyReviewable, roundsRequired, query.Page, query.PageSize, ct);

        var items = rows
            .Select(x => new TaskReviewItemDto(
                x.RecordingId,
                x.SpeakerId,
                x.ScriptId,
                x.SentenceVariant,
                CodeSwitchText.Strip(x.ScriptTagged),
                x.DurationSec,
                x.Status,
                x.QueueStatus,
                x.ReviewsDone,
                roundsRequired,
                x.MyReviewDone,
                ReviewRules.BlockerFor(
                    x.SpeakerId == reviewerId, x.MyReviewDone, x.Status, x.ReviewsDone, roundsRequired)))
            .ToList();

        return new PagedResult<TaskReviewItemDto>(items, query.Page, query.PageSize, total);
    }

    public async Task<NextReviewDto> GetForReviewAsync(
        string recordingId, long reviewerId, CancellationToken ct = default)
    {
        var recording = await reviews.GetWithScriptAsync(recordingId, ct)
                        ?? throw new NotFoundException(
                            "recording_not_found", $"Không tìm thấy bản ghi {recordingId}.");

        var existing = await reviews.GetReviewsAsync(recordingId, ct);
        var roundsRequired = await RoundsRequiredAsync(ct);

        var blocker = ReviewRules.BlockerFor(
            recording.SpeakerId == reviewerId,
            existing.Any(v => v.ReviewerId == reviewerId),
            recording.Status,
            existing.Count,
            roundsRequired);

        // Tách mã lỗi theo từng lý do, trùng với mã mà POST reviews trả về, để FE xử lý một kiểu
        // cho cả hai chỗ: 403 là không có quyền, 409 là người khác đã làm trước.
        switch (blocker)
        {
            case ReviewBlocker.OwnRecording:
                throw new ForbiddenException(
                    "self_review_forbidden", "Bạn không được duyệt bản ghi do chính mình thu.");

            case ReviewBlocker.AlreadyReviewedByMe:
                throw new ConflictException(
                    "already_reviewed",
                    $"Bạn đã duyệt bản ghi {recordingId} rồi. Mỗi người chỉ được duyệt một lần.");

            case ReviewBlocker.NotPendingReview:
                throw new ConflictException(
                    "recording_not_reviewable",
                    $"Bản ghi {recordingId} đang ở trạng thái {recording.Status}, không còn chờ duyệt.");

            case ReviewBlocker.RoundsFull:
                throw new ConflictException(
                    "no_more_rounds", $"Bản ghi {recordingId} đã đủ {roundsRequired} lượt duyệt.");
        }

        return await BuildPayloadAsync(recording, existing.Count, roundsRequired, ct);
    }

    public async Task<SubmitReviewResult> SubmitAsync(
        string recordingId, long reviewerId, SubmitReviewRequest request, CancellationToken ct = default)
    {
        var decision = request.Decision!.Value;

        var codes = request.RejectionReasonCodes
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c.Trim())
            .Distinct()
            .ToArray();

        // Kiểm phần không cần database trước, trước cả khi mở transaction.
        if (decision == ReviewDecision.Rejected && codes.Length == 0)
        {
            throw new UnprocessableException(
                "rejection_reason_required", "Từ chối bản ghi thì phải chọn ít nhất một lý do.");
        }

        if (decision == ReviewDecision.Approved && codes.Length > 0)
        {
            throw new UnprocessableException(
                "reasons_not_allowed", "Duyệt đạt thì không được kèm lý do từ chối.");
        }

        List<RejectionReason> reasons = codes.Length == 0 ? [] : await LoadActiveReasonsAsync(codes, ct);
        var roundsRequired = await RoundsRequiredAsync(ct);

        // Lỗi ném ra ở bất kỳ đâu bên dưới → transaction bị huỷ khi thoát khối, không ghi gì cả.
        await using var transaction = await reviews.BeginTransactionAsync(ct);

        var recording = await reviews.LockRecordingAsync(recordingId, ct)
                        ?? throw new NotFoundException(
                            "recording_not_found", $"Không tìm thấy bản ghi {recordingId}.");

        if (recording.Status != RecordingStatus.PendingReview)
        {
            throw new ConflictException(
                "recording_not_reviewable",
                $"Bản ghi {recordingId} đang ở trạng thái {recording.Status}, không còn chờ duyệt.");
        }

        // Kiểm ở đây để trả lỗi rõ ràng. Trigger trg_review_no_self dưới database là lưới chắn cuối.
        if (recording.SpeakerId == reviewerId)
        {
            throw new ForbiddenException("self_review_forbidden", "Bạn không được duyệt bản ghi do chính mình thu.");
        }

        var previous = await reviews.GetReviewsAsync(recordingId, ct);

        if (previous.Any(v => v.ReviewerId == reviewerId))
        {
            throw new ConflictException(
                "already_reviewed",
                $"Bạn đã duyệt bản ghi {recordingId} rồi. Mỗi người chỉ được duyệt một lần.");
        }

        var round = ReviewRules.NextRound(previous.Count);

        if (round > roundsRequired)
        {
            throw new ConflictException(
                "no_more_rounds", $"Bản ghi {recordingId} đã đủ {roundsRequired} lượt duyệt.");
        }

        // Khoá hàng ở trên chỉ bảo đảm hai người không ghi CÙNG LÚC; nó không biết người đến sau
        // đang nhìn thấy vòng mấy. Thiếu bước so này thì quyết định của họ bị ghi sang vòng khác.
        if (request.ExpectedRound != round)
        {
            throw new ConflictException(
                "round_changed",
                $"Bản ghi {recordingId} đã chuyển sang vòng {round} trong lúc bạn duyệt vòng {request.ExpectedRound}. " +
                "Hãy lấy lại bản ghi.");
        }

        var taskRecording = await ResolveTaskAsync(request.TaskId, reviewerId, recordingId, ct);

        var review = new Review
        {
            RecordingId = recordingId,
            ReviewerId = reviewerId,
            TaskId = taskRecording?.TaskId,
            ReviewRound = (short)round,
            Decision = decision,
            IsBlind = await config.GetBoolAsync(ConfigKeys.ReviewDefaultBlind, true, ct),
            Comment = string.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment.Trim(),
            ReviewedAt = clock.GetUtcNow(),
            RejectionReasons = [.. reasons.Select(r => new ReviewRejectionReason { ReasonId = r.ReasonId })]
        };

        reviews.Add(review);

        if (taskRecording is not null)
        {
            taskRecording.Status = TaskRecordingStatus.Reviewed;
        }

        await reviews.SaveChangesAsync(ct);

        // KHÔNG tự đặt trạng thái bản ghi: trigger trg_review_majority của database vừa làm việc đó
        // nếu đã đủ lượt. Đọc lại để biết kết quả — thực thể đang theo dõi vẫn giữ giá trị cũ.
        var status = await reviews.GetRecordingStatusAsync(recordingId, ct);

        await taskTracker.OnReviewSubmittedAsync(recording, status, taskRecording?.TaskId, ct);

        await transaction.CommitAsync(ct);

        return new SubmitReviewResult(
            review.ReviewId, round, roundsRequired, previous.Count + 1,
            status, status != RecordingStatus.PendingReview);
    }

    public async Task<IReadOnlyList<ReviewDto>> GetHistoryAsync(
        string recordingId, long viewerId, ViewerRole role, CancellationToken ct = default)
    {
        var recording = await recordings.GetAsync(recordingId, ct);

        // Speaker hỏi bản không phải của mình thì trả 404 như thể không tồn tại.
        if (recording is null || (role == ViewerRole.Speaker && recording.SpeakerId != viewerId))
        {
            throw new NotFoundException("recording_not_found", $"Không tìm thấy bản ghi {recordingId}.");
        }

        var all = await reviews.GetReviewsAsync(recordingId, ct);
        var isFinal = recording.Status is RecordingStatus.Approved or RecordingStatus.Rejected;

        IEnumerable<Review> visible = role switch
        {
            ViewerRole.Manager => all,

            // Mọi vòng đều duyệt mù, nên khi bản ghi còn đang duyệt thì Reviewer chỉ thấy lượt của mình.
            ViewerRole.Reviewer => isFinal ? all : all.Where(v => v.ReviewerId == viewerId),

            // Speaker chỉ thấy kết quả sau khi đã chốt, để biết vì sao bị từ chối mà thu lại cho đúng.
            ViewerRole.Speaker => isFinal ? all : [],

            _ => []
        };

        // Speaker không được biết ai đã duyệt bản của mình.
        return [.. visible.Select(v => v.ToDto(includeReviewer: role != ViewerRole.Speaker))];
    }

    public async Task<ReviewerProgressDto> GetProgressAsync(long reviewerId, CancellationToken ct = default)
    {
        var now = clock.GetUtcNow();
        var tasks = await reviews.GetActiveTaskProgressAsync(reviewerId, ct);

        var items = tasks
            .Select(t => new ReviewTaskProgressDto(
                t.TaskId,
                t.Description,
                t.TargetQty,
                t.Done,
                WorkTasks.TaskStateMachine.Percent(t.Done, t.TargetQty),
                t.Deadline,
                t.Deadline.HasValue ? Math.Round((t.Deadline.Value - now).TotalHours, 1) : null,
                t.Deadline.HasValue && t.Deadline.Value < now))
            .ToList();

        return new ReviewerProgressDto(await reviews.CountReviewsByAsync(reviewerId, ct), items);
    }

    // ----------------------------------------------------------------- nội bộ

    /// <summary>
    /// Dữ liệu một Reviewer cần để duyệt: câu đối chiếu và link nghe tạm. Dùng chung cho bản do
    /// hệ thống phát (GET next) và bản người ta tự chọn trong task — hai đường vào phải trả y như nhau.
    /// </summary>
    private async Task<NextReviewDto> BuildPayloadAsync(
        Recording recording, int reviewsSoFar, int roundsRequired, CancellationToken ct)
    {
        var key = storage.GetObjectKey(recording.CloudLink);

        if (key is null)
        {
            throw new UnprocessableException(
                "external_recording",
                $"Bản ghi {recording.RecordingId} trỏ tới kho lưu trữ khác, không nghe được qua hệ thống.");
        }

        var (url, expiresAt) = await storage.GetDownloadUrlAsync(key, ct);

        // Bản cs đọc câu chen tiếng Anh, bản vi đọc câu thuần Việt — đưa đúng câu cần đối chiếu.
        var tagged = recording.SentenceVariant == SentenceVariant.CodeSwitching
            ? recording.Script.CsContent
            : recording.Script.ViContent;

        return new NextReviewDto(
            recording.RecordingId,
            recording.SpeakerId,
            ReviewRules.NextRound(reviewsSoFar),
            roundsRequired,
            await config.GetBoolAsync(ConfigKeys.ReviewDefaultBlind, true, ct),
            recording.ScriptId,
            recording.SentenceVariant,
            CodeSwitchText.Strip(tagged),
            tagged,
            recording.DurationSec,
            url,
            expiresAt);
    }

    /// <summary>
    /// Số lượt duyệt cần có. Trigger của database ghim cứng con số 3, nên đặt tham số khác đi
    /// là hai bên lệch nhau — cảnh báo ngay thay vì để dữ liệu sai âm thầm.
    /// </summary>
    private async Task<int> RoundsRequiredAsync(CancellationToken ct)
    {
        var rounds = await config.GetIntAsync(
            ConfigKeys.ReviewRoundsRequired, ReviewRules.RoundsRequiredInDatabase, ct);

        if (rounds != ReviewRules.RoundsRequiredInDatabase)
        {
            logger.LogWarning(
                "Tham số {Key} đang là {Value} nhưng trigger trg_review_majority chốt ở {Expected} lượt. " +
                "Phải sửa trigger trong docs/codeswitchlabel.sql cho khớp.",
                ConfigKeys.ReviewRoundsRequired, rounds, ReviewRules.RoundsRequiredInDatabase);
        }

        return rounds;
    }

    private async Task<TaskRecording?> ResolveTaskAsync(
        long? taskId, long reviewerId, string recordingId, CancellationToken ct)
    {
        if (taskId.HasValue)
        {
            return await reviews.GetQueuedTaskRecordingAsync(taskId.Value, reviewerId, recordingId, ct)
                   ?? throw new ForbiddenException(
                       "task_not_reviewable",
                       $"Task #{taskId} không phải task duyệt đang giao cho bạn, hoặc bản ghi {recordingId} không còn chờ trong task đó.");
        }

        // Không gửi taskId nhưng bản ghi đang Queued trong task của chính reviewer
        // thì tự gắn vào task đó để tiến độ Done không bị lệch (spot-check chỉ khi
        // bản ghi không nằm trong task nào của người này).
        var queued = await reviews.GetQueuedItemsAsync(recordingId, ct);

        foreach (var item in queued)
        {
            if (await reviews.IsActiveReviewTaskOfAsync(item.TaskId, reviewerId, ct))
            {
                var mine = await reviews.GetQueuedTaskRecordingAsync(item.TaskId, reviewerId, recordingId, ct);
                if (mine is not null) return mine;
            }
        }

        return null;
    }

    private async Task<List<RejectionReason>> LoadActiveReasonsAsync(string[] codes, CancellationToken ct)
    {
        var found = await reviews.GetReasonsByCodesAsync(codes, ct);

        var unknown = codes.Except(found.Select(r => r.ReasonCode)).ToList();
        if (unknown.Count > 0)
        {
            throw new UnprocessableException(
                "rejection_reason_unknown", $"Không có lý do nào mang mã: {string.Join(", ", unknown)}.");
        }

        var inactive = found.Where(r => !r.IsActive).Select(r => r.ReasonCode).ToList();
        if (inactive.Count > 0)
        {
            throw new UnprocessableException(
                "rejection_reason_inactive", $"Lý do đã bị ẩn, không dùng được nữa: {string.Join(", ", inactive)}.");
        }

        return found;
    }
}
