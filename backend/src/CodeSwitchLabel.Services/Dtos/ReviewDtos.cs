using System.ComponentModel.DataAnnotations;
using CodeSwitchLabel.Repositories.Enums;
using CodeSwitchLabel.Services.Reviews;

namespace CodeSwitchLabel.Services.Dtos;

public record ReviewReasonDto(string Code, RejectionCategory Category, string? Description);

/// <param name="ReviewerId">Null khi người xem không được biết ai đã duyệt — ví dụ Speaker xem kết quả bản của mình.</param>
public record ReviewDto(
    long ReviewId,
    int Round,
    ReviewRoundKind Kind,
    long? ReviewerId,
    ReviewDecision Decision,
    bool IsBlind,
    string? Comment,
    IReadOnlyList<ReviewReasonDto> Reasons,
    DateTimeOffset ReviewedAt);

/// <summary>Bản ghi phát cho Reviewer, kèm đủ thứ để duyệt mà không phải gọi thêm API.</summary>
/// <param name="PreviousReviews">Chỉ có ở vòng phân xử. Vòng kiểm tra mù thì luôn rỗng.</param>
public record NextReviewDto(
    long RecordingId,
    long SpeakerId,
    int Round,
    ReviewRoundKind Kind,
    bool IsBlind,
    long ScriptId,
    string ScriptContent,
    int EnWordCount,
    decimal DurationSec,
    string AudioUrl,
    DateTimeOffset AudioUrlExpiresAt,
    IReadOnlyList<ReviewDto> PreviousReviews);

public record SubmitReviewRequest
{
    [Required(ErrorMessage = "Phải chọn duyệt đạt hoặc từ chối.")]
    public ReviewDecision? Decision { get; init; }

    /// <summary>
    /// Vòng mà người duyệt đã thấy lúc nhận bản ghi — chép nguyên trường round của GET next.
    /// Nếu trong lúc đang nghe, người khác đã duyệt xong vòng đó, server so thấy lệch và trả 409
    /// thay vì âm thầm ghi quyết định này vào vòng sau.
    /// </summary>
    [Required(ErrorMessage = "Thiếu expectedRound — lấy từ trường round của GET /api/reviewer/recordings/next.")]
    [Range(1, 3)]
    public int? ExpectedRound { get; init; }

    /// <summary>Bắt buộc ít nhất một mã khi từ chối; phải để trống khi duyệt đạt.</summary>
    public List<string> RejectionReasonCodes { get; init; } = [];

    [StringLength(1000)]
    public string? Comment { get; init; }

    /// <summary>Chỉ dùng cho vòng 1 khi duyệt trong một task. Vòng kiểm tra và phân xử không thuộc task.</summary>
    public long? TaskId { get; init; }
}

/// <param name="IsFinal">Bản ghi đã chốt trạng thái cuối, hay còn chờ vòng sau.</param>
public record SubmitReviewResult(
    long ReviewId,
    int Round,
    ReviewRoundKind Kind,
    RecordingStatus RecordingStatus,
    bool IsFinal);

/// <summary>Backend tính sẵn cả phần trăm lẫn thời gian còn lại — frontend không tự chia, để mọi màn hình ra cùng một con số.</summary>
public record ReviewTaskProgressDto(
    long TaskId,
    string? Description,
    int TargetQty,
    int Done,
    int Percent,
    DateTimeOffset Deadline,
    double HoursRemaining,
    bool IsOverdue);

public record ReviewerProgressDto(int TotalReviews, IReadOnlyList<ReviewTaskProgressDto> ActiveTasks);
