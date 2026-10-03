using System.ComponentModel.DataAnnotations;
using CodeSwitchLabel.Repositories.Enums;
using CodeSwitchLabel.Services.Reviews;

namespace CodeSwitchLabel.Services.Dtos;

public record ReviewReasonDto(string Code, RejectionCategory Category, string? Description);

/// <param name="ReviewerId">Null khi người xem không được biết ai đã duyệt — ví dụ Speaker xem bản của mình.</param>
public record ReviewDto(
    long ReviewId,
    int Round,
    long? ReviewerId,
    ReviewDecision Decision,
    bool IsBlind,
    string? Comment,
    IReadOnlyList<ReviewReasonDto> Reasons,
    DateTimeOffset ReviewedAt);

/// <summary>
/// Bản ghi phát cho Reviewer. Mọi vòng đều là duyệt mù nên KHÔNG kèm ý kiến của người khác.
/// </summary>
/// <param name="ScriptText">Câu cần đối chiếu, đã bỏ nhãn: bản cs hay bản thuần Việt tuỳ biến thể.</param>
/// <param name="ScriptTagged">Cũng câu đó nhưng còn nhãn [vi]/[en], để giao diện tô màu phần tiếng Anh.</param>
public record NextReviewDto(
    string RecordingId,
    long SpeakerId,
    int Round,
    int RoundsRequired,
    bool IsBlind,
    string ScriptId,
    SentenceVariant SentenceVariant,
    string ScriptText,
    string ScriptTagged,
    decimal DurationSec,
    string AudioUrl,
    DateTimeOffset AudioUrlExpiresAt);

public record SubmitReviewRequest
{
    [Required(ErrorMessage = "Phải chọn duyệt đạt hoặc từ chối.")]
    public ReviewDecision? Decision { get; init; }

    /// <summary>
    /// Vòng mà người duyệt thấy lúc nhận bản ghi — chép nguyên trường round của GET next.
    /// Lệch thì server trả 409 thay vì ghi quyết định này vào vòng khác.
    /// </summary>
    [Required(ErrorMessage = "Thiếu expectedRound — lấy từ trường round của GET /api/reviewer/recordings/next.")]
    [Range(1, 3)]
    public int? ExpectedRound { get; init; }

    /// <summary>Bắt buộc ít nhất một mã khi từ chối; phải để trống khi duyệt đạt.</summary>
    public List<string> RejectionReasonCodes { get; init; } = [];

    [StringLength(1000)]
    public string? Comment { get; init; }

    /// <summary>Điền khi đang duyệt trong một task được giao.</summary>
    public long? TaskId { get; init; }
}

/// <param name="ReviewsSoFar">Số lượt đã có, kể cả lượt vừa ghi.</param>
/// <param name="IsFinal">Đã đủ số vòng và database đã chốt trạng thái cuối.</param>
public record SubmitReviewResult(
    long ReviewId,
    int Round,
    int RoundsRequired,
    int ReviewsSoFar,
    RecordingStatus RecordingStatus,
    bool IsFinal);

public record ReviewTaskProgressDto(
    long TaskId,
    string? Description,
    int TargetQty,
    int Done,
    int Percent,
    DateTimeOffset? Deadline,
    double? HoursRemaining,
    bool IsOverdue);

public record ReviewerProgressDto(int TotalReviews, IReadOnlyList<ReviewTaskProgressDto> ActiveTasks);

/// <summary>Tham số của danh sách bản ghi trong một task duyệt.</summary>
public record TaskReviewQuery : PageRequest
{
    /// <summary>
    /// Mặc định trả cả task: bản chưa duyệt lẫn bản đã duyệt, để Reviewer thấy mình đi tới đâu.
    /// Đặt true thì chỉ còn những bản bấm vào duyệt được ngay.
    /// </summary>
    public bool OnlyReviewable { get; init; }
}

/// <summary>
/// Một bản ghi trong task duyệt, nhìn từ phía Reviewer.
///
/// KHÔNG kèm quyết định của bất kỳ ai — luật duyệt mù. Chỉ nói đã có mấy lượt trên tổng số cần có,
/// và người đang xem đã duyệt bản này chưa. Biết "2/3 lượt" không cho biết hai lượt kia đạt hay trượt.
/// </summary>
/// <param name="ScriptText">Câu cần đối chiếu, đã bỏ nhãn — đủ để hiện trong danh sách.</param>
/// <param name="QueueStatus">Trạng thái của bản ghi trong hàng chờ của task.</param>
/// <param name="ReviewsDone">Số lượt đã có, không kèm ai duyệt và duyệt thế nào.</param>
/// <param name="MyReviewDone">Chính người đang xem đã duyệt bản này rồi.</param>
/// <param name="Blocker">Lý do chưa duyệt được; <c>None</c> là mở ra duyệt được ngay.</param>
public record TaskReviewItemDto(
    string RecordingId,
    long SpeakerId,
    string ScriptId,
    SentenceVariant SentenceVariant,
    string ScriptText,
    decimal DurationSec,
    RecordingStatus Status,
    TaskRecordingStatus QueueStatus,
    int ReviewsDone,
    int RoundsRequired,
    bool MyReviewDone,
    ReviewBlocker Blocker)
{
    /// <summary>Có mở được bằng <c>GET /api/reviewer/recordings/{id}</c> không.</summary>
    public bool CanReview => Blocker == ReviewBlocker.None;
}
