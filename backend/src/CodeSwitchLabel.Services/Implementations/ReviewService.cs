using CodeSwitchLabel.Repositories.Entities;
using CodeSwitchLabel.Repositories.Enums;
using CodeSwitchLabel.Repositories.Repositories;
using CodeSwitchLabel.Repositories.Storage;
using CodeSwitchLabel.Services.Abstractions;
using CodeSwitchLabel.Services.Common;
using CodeSwitchLabel.Services.Dtos;
using CodeSwitchLabel.Services.Reviews;

namespace CodeSwitchLabel.Services.Implementations;

internal static class ReviewMapper
{
    public static ReviewDto ToDto(this Review v, bool includeReviewer) =>
        new(v.ReviewId,
            v.ReviewRound,
            ReviewStateMachine.KindOf(v.ReviewRound),
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
    IReviewSampler sampler,
    ISystemConfigService config,
    TimeProvider clock) : IReviewService
{
    public async Task<NextReviewDto?> GetNextAsync(
        long reviewerId, long? taskId, long? speakerId, bool random, CancellationToken ct = default)
    {
        if (taskId.HasValue && !await reviews.IsActiveReviewTaskOfAsync(taskId.Value, reviewerId, ct))
        {
            throw new ForbiddenException(
                "task_not_reviewable", $"Task #{taskId} không phải task duyệt đang giao cho bạn.");
        }

        var recording = await reviews.GetNextForReviewerAsync(reviewerId, taskId, speakerId, random, ct);
        if (recording is null) return null;

        var round = recording.Reviews.Count + 1;
        var kind = ReviewStateMachine.KindOf(round);
        var (url, expiresAt) = await storage.GetDownloadUrlAsync(recording.S3Key, ct);

        // Vòng kiểm tra mù: tuyệt đối không lộ gì của vòng trước.
        // Vòng phân xử: phải thấy cả hai ý kiến thì mới phân xử được.
        IReadOnlyList<ReviewDto> previous = kind == ReviewRoundKind.Adjudication
            ? [.. recording.Reviews.OrderBy(v => v.ReviewRound).Select(v => v.ToDto(includeReviewer: true))]
            : [];

        return new NextReviewDto(
            recording.RecordingId,
            recording.SpeakerId,
            round,
            kind,
            ReviewStateMachine.IsBlind(round),
            recording.ScriptId,
            recording.Script.Content,
            recording.Script.EnWordCount,
            recording.DurationSec,
            url,
            expiresAt,
            previous);
    }

    public async Task<SubmitReviewResult> SubmitAsync(
        long recordingId, long reviewerId, SubmitReviewRequest request, CancellationToken ct = default)
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

        // Lỗi ném ra ở bất kỳ đâu bên dưới → transaction bị huỷ khi thoát khối, không ghi gì cả.
        await using var transaction = await reviews.BeginTransactionAsync(ct);

        var recording = await reviews.LockRecordingAsync(recordingId, ct)
                        ?? throw new NotFoundException("recording_not_found", $"Không tìm thấy bản ghi #{recordingId}.");

        if (recording.Status != RecordingStatus.PendingReview)
        {
            throw new ConflictException(
                "recording_not_reviewable",
                $"Bản ghi #{recordingId} đang ở trạng thái {recording.Status}, không còn chờ duyệt.");
        }

        // Kiểm ở đây để trả lỗi rõ ràng cho người dùng.
        // Trigger trg_review_not_self dưới database là lưới chắn cuối, kể cả khi ai đó ghi thẳng bằng SQL.
        if (recording.SpeakerId == reviewerId)
        {
            throw new ForbiddenException("self_review_forbidden", "Bạn không được duyệt bản ghi do chính mình thu.");
        }

        var previous = await reviews.GetReviewsAsync(recordingId, ct);

        if (previous.Any(v => v.ReviewerId == reviewerId))
        {
            throw new ConflictException(
                "already_reviewed",
                $"Bạn đã duyệt bản ghi #{recordingId} ở vòng trước. Mỗi vòng phải do một người khác duyệt.");
        }

        var round = previous.Count + 1;

        if (round > ReviewStateMachine.MaxRound)
        {
            throw new ConflictException(
                "no_more_rounds", $"Bản ghi #{recordingId} đã đủ {ReviewStateMachine.MaxRound} vòng duyệt.");
        }

        // Khoá hàng ở trên chỉ bảo đảm hai người không ghi CÙNG LÚC; nó không biết người đến sau
        // đang nhìn thấy vòng mấy. Thiếu bước so này, ca hai người cùng nhận một bản ở vòng 1 mà bản đó
        // bị rút mẫu thì người thứ hai bị âm thầm ghi thành vòng 2 — dù lúc nghe họ tưởng mình làm vòng 1.
        if (request.ExpectedRound != round)
        {
            throw new ConflictException(
                "round_changed",
                $"Bản ghi #{recordingId} đã chuyển sang vòng {round} trong lúc bạn duyệt vòng {request.ExpectedRound}. " +
                "Hãy lấy lại bản ghi.");
        }

        var kind = ReviewStateMachine.KindOf(round);
        var taskRecording = await ResolveTaskAsync(request.TaskId, kind, reviewerId, recordingId, ct);

        var selectedForSpotCheck = kind == ReviewRoundKind.Primary &&
            sampler.ShouldSpotCheck(await config.GetDecimalAsync(ConfigKeys.ReviewRandomRatio, 0.20m, ct));

        var newStatus = ReviewStateMachine.NextStatus(
            round, decision, [.. previous.Select(v => v.Decision)], selectedForSpotCheck);

        var review = new Review
        {
            RecordingId = recordingId,
            ReviewerId = reviewerId,
            TaskId = taskRecording?.TaskId,
            ReviewRound = (short)round,
            Decision = decision,
            IsBlind = ReviewStateMachine.IsBlind(round),
            Comment = string.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment.Trim(),
            ReviewedAt = clock.GetUtcNow(),
            RejectionReasons = [.. reasons.Select(r => new ReviewRejectionReason { ReasonId = r.ReasonId })]
        };

        reviews.Add(review);
        recording.Status = newStatus;

        if (taskRecording is not null)
        {
            taskRecording.Status = TaskRecordingStatus.Reviewed;
        }

        await reviews.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return new SubmitReviewResult(
            review.ReviewId, round, kind, newStatus, newStatus != RecordingStatus.PendingReview);
    }

    public async Task<IReadOnlyList<ReviewDto>> GetHistoryAsync(
        long recordingId, long viewerId, ViewerRole role, CancellationToken ct = default)
    {
        var recording = await recordings.GetAsync(recordingId, ct);

        // Speaker hỏi bản không phải của mình thì trả 404 như thể không tồn tại — giống module Recording.
        if (recording is null || (role == ViewerRole.Speaker && recording.SpeakerId != viewerId))
        {
            throw new NotFoundException("recording_not_found", $"Không tìm thấy bản ghi #{recordingId}.");
        }

        var all = await reviews.GetReviewsAsync(recordingId, ct);
        var isFinal = recording.Status is RecordingStatus.Approved or RecordingStatus.Rejected;

        IEnumerable<Review> visible = role switch
        {
            ViewerRole.Manager => all,

            // Còn đang duyệt thì Reviewer chỉ thấy lượt của chính mình. Nếu không, người sắp làm
            // vòng kiểm tra mù có thể xem trước kết quả vòng 1 — và "duyệt mù" chỉ còn là cái tên.
            ViewerRole.Reviewer => isFinal ? all : all.Where(v => v.ReviewerId == viewerId),

            // Speaker chỉ thấy kết quả sau khi đã chốt, để biết vì sao bị từ chối mà thu lại cho đúng.
            ViewerRole.Speaker => isFinal ? all : Enumerable.Empty<Review>(),

            _ => Enumerable.Empty<Review>()
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
                t.TargetQty == 0 ? 0 : (int)Math.Min(100, Math.Round(100.0 * t.Done / t.TargetQty)),
                t.Deadline,
                Math.Round((t.Deadline - now).TotalHours, 1),
                t.Deadline < now))
            .ToList();

        return new ReviewerProgressDto(await reviews.CountReviewsByAsync(reviewerId, ct), items);
    }

    private async Task<TaskRecording?> ResolveTaskAsync(
        long? taskId, ReviewRoundKind kind, long reviewerId, long recordingId, CancellationToken ct)
    {
        if (!taskId.HasValue) return null;

        // Vòng kiểm tra và vòng phân xử nằm ngoài task — ERD ghi rõ task_id NULL nghĩa là lượt kiểm tra ngẫu nhiên.
        if (kind != ReviewRoundKind.Primary)
        {
            throw new UnprocessableException(
                "task_not_allowed_for_round",
                $"Bản ghi #{recordingId} đang cần vòng {(int)kind} ({kind}). Vòng này không thuộc task nào — hãy bỏ taskId.");
        }

        return await reviews.GetQueuedTaskRecordingAsync(taskId.Value, reviewerId, recordingId, ct)
               ?? throw new ForbiddenException(
                   "task_not_reviewable",
                   $"Task #{taskId} không phải task duyệt đang giao cho bạn, hoặc bản ghi #{recordingId} không còn chờ trong task đó.");
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
