using CodeSwitchLabel.Repositories.Enums;

namespace CodeSwitchLabel.Services.Abstractions;

/// <param name="Verdict">approved hoặc rejected. Máy là người phá thế nên luôn phải chọn một bên.</param>
/// <param name="EffectiveWer">Con số đã đem phán: nhỏ hơn giữa <paramref name="Wer"/> và <paramref name="WerFolded"/>.</param>
/// <param name="RecordingStatus">Trạng thái bản ghi sau khi trigger đa số chạy — phiếu này là lượt thứ ba nên bản ghi chốt luôn.</param>
public record AiReviewResultDto(
    string RecordingId,
    string Verdict,
    decimal Wer,
    decimal WerFolded,
    decimal EffectiveWer,
    string Transcript,
    string ExpectedText,
    string Explanation,
    string Model,
    long ReviewId,
    RecordingStatus? RecordingStatus,
    DateTimeOffset AnalyzedAt);

/// <param name="Code">Mã lỗi nghiệp vụ, ví dụ recording_not_reviewable hay ai_review_not_tiebreak.</param>
public record AiReviewSkippedDto(string RecordingId, string Code, string Message);

/// <summary>
/// Kết quả một lượt chạy lô. Bản bị bỏ qua KHÔNG ghi dòng bằng chứng nào, nên lượt sau
/// vẫn thử lại được nếu nó còn đang ở thế hai người lệch nhau.
/// </summary>
public record AiReviewBatchResultDto(
    int Analyzed,
    int Approved,
    int Rejected,
    IReadOnlyList<AiReviewResultDto> Items,
    IReadOnlyList<AiReviewSkippedDto> Skipped);

/// <summary>
/// Một bản ghi đang chờ máy phán, kèm đủ dữ liệu để giao diện dựng bảng ngay.
/// </summary>
/// <param name="ExpectedText">Văn bản đáng ra phải đọc, đã bỏ nhãn <c>[vi]</c>/<c>[en]</c>.</param>
/// <param name="Reviews">Hai lượt duyệt của người đã làm nên thế lệch này.</param>
public record AiReviewPendingDto(
    string RecordingId,
    SentenceVariant Variant,
    decimal DurationSec,
    string ExpectedText,
    DateTimeOffset RecordedAt,
    IReadOnlyList<AiReviewPendingVoteDto> Reviews);

/// <summary>Một lượt duyệt của người trên bản ghi đang chờ máy.</summary>
public record AiReviewPendingVoteDto(
    short Round,
    long ReviewerId,
    ReviewDecision Decision,
    DateTimeOffset ReviewedAt);

public interface IAiReviewService
{
    /// <summary>
    /// Phán một bản ghi đang ở thế hai Reviewer người lệch nhau: lấy audio từ kho, chuyển
    /// thành chữ, so với văn bản, rồi bỏ phiếu thứ ba qua đúng
    /// <see cref="IReviewService.SubmitAsync"/> mà người dùng đi.
    ///
    /// Chưa tới lượt máy thì ném <c>ai_review_not_tiebreak</c> và KHÔNG gọi ASR.
    /// </summary>
    Task<AiReviewResultDto> AnalyzeAsync(string recordingId, CancellationToken ct = default);

    /// <summary>
    /// Chạy lô trên các bản đang ở thế lệch mà máy chưa phán. Mỗi bản là một lần gọi ASR có
    /// tính phí, nên <paramref name="limit"/> là trần bắt buộc, không phải gợi ý.
    /// </summary>
    Task<AiReviewBatchResultDto> RunBatchAsync(int limit, CancellationToken ct = default);

    /// <summary>
    /// Các bản ghi đang ở thế hai người duyệt lệch nhau mà máy chưa phán — tức đúng tập
    /// mà <see cref="RunBatchAsync"/> sẽ xử lý, nhưng chỉ đọc, không phán gì.
    /// </summary>
    Task<IReadOnlyList<AiReviewPendingDto>> GetPendingAsync(int limit, CancellationToken ct = default);

    /// <summary>Bằng chứng gần nhất của một bản ghi. Null nghĩa là máy chưa từng phán bản này.</summary>
    Task<AiReviewResultDto?> GetLatestAsync(string recordingId, CancellationToken ct = default);
}
