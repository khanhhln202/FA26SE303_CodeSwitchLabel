using CodeSwitchLabel.Repositories.Enums;

namespace CodeSwitchLabel.Services.Abstractions;

/// <param name="Verdict">approved, rejected hoặc abstained.</param>
/// <param name="EffectiveWer">Con số đã đem phán: nhỏ hơn giữa <paramref name="Wer"/> và <paramref name="WerFolded"/>.</param>
/// <param name="ReviewId">Null khi máy bỏ quyền — không có lượt duyệt nào được ghi.</param>
/// <param name="RecordingStatus">Trạng thái bản ghi sau khi trigger đa số chạy. Null khi bỏ quyền.</param>
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
    long? ReviewId,
    RecordingStatus? RecordingStatus,
    DateTimeOffset AnalyzedAt);

/// <param name="Code">Mã lỗi nghiệp vụ, ví dụ recording_not_reviewable hay round_changed.</param>
public record AiReviewSkippedDto(string RecordingId, string Code, string Message);

/// <summary>
/// Kết quả một lượt chạy lô. Bản bị bỏ qua KHÔNG ghi dòng bằng chứng nào, nên lượt sau
/// vẫn thử lại được nếu nó còn ở trạng thái chờ duyệt.
/// </summary>
public record AiReviewBatchResultDto(
    int Analyzed,
    int Approved,
    int Rejected,
    int Abstained,
    IReadOnlyList<AiReviewResultDto> Items,
    IReadOnlyList<AiReviewSkippedDto> Skipped);

public interface IAiReviewService
{
    /// <summary>
    /// Phán một bản ghi: lấy audio từ kho, chuyển thành chữ, so với văn bản, rồi bỏ phiếu
    /// qua đúng <see cref="IReviewService.SubmitAsync"/> mà người dùng đi — hoặc bỏ quyền.
    /// </summary>
    Task<AiReviewResultDto> AnalyzeAsync(string recordingId, CancellationToken ct = default);

    /// <summary>
    /// Chạy lô trên các bản còn chờ duyệt mà máy chưa phán. Mỗi bản là một lần gọi ASR có
    /// tính phí, nên <paramref name="limit"/> là trần bắt buộc, không phải gợi ý.
    /// </summary>
    Task<AiReviewBatchResultDto> RunBatchAsync(int limit, CancellationToken ct = default);

    /// <summary>Bằng chứng gần nhất của một bản ghi. Null nghĩa là máy chưa từng phán bản này.</summary>
    Task<AiReviewResultDto?> GetLatestAsync(string recordingId, CancellationToken ct = default);
}
