using CodeSwitchLabel.Repositories.Enums;

namespace CodeSwitchLabel.Services.Dtos;

public record ReviewerTotalsDto(int ApprovedCount, int RejectedCount);

public record RejectReasonStatDto(string Label, double Percentage, string Count);

public record CategoryBreakdownDto(string Category, int Count);

public record TopRejectedSentenceDto(
    string ScriptId,
    string Text,
    string? ViText,
    IReadOnlyList<string> EnglishWords,
    string? Task,
    int Count,
    IReadOnlyList<CategoryBreakdownDto> Breakdown,
    string? Example);

public record TopRejectedSpeakerDto(
    long SpeakerId,
    string Name,
    int Count,
    string? TopReason,
    IReadOnlyList<CategoryBreakdownDto> Breakdown,
    string? Example);

/// <summary>
/// Một CẶP bản ghi tôi đã duyệt — cùng cách gom với lịch sử ghi âm của Speaker: kèm cả hai
/// câu, cả hai link nghe tạm, còn <c>myDecision</c> là quyết định của tôi cho cả cặp.
/// </summary>
/// <param name="MyDecision">Có lượt từ chối bất kỳ bản nào thì cả cặp là "Rejected", ngược lại "Approved".</param>
/// <param name="RecordingStatus">Trạng thái của CẶP, gộp từ hai bản — cùng luật với lịch sử ghi âm của Speaker.</param>
/// <param name="ReviewedAt">Mốc duyệt gần nhất của tôi trong cặp; cũng là khoá sắp xếp mặc định.</param>
/// <param name="Reviews">Các lượt đã chốt của cặp, gom theo vòng; rỗng khi cặp còn đang duyệt — luật duyệt mù.</param>
public record ReviewerHistoryItemDto(
    string ScriptId,
    long SpeakerId,
    string? SpeakerName,
    string? TaskTitle,
    string CsText,
    string ViText,
    string? CsRecordingId,
    string? ViRecordingId,
    string? CsAudioUrl,
    string? ViAudioUrl,
    decimal DurationSec,
    string MyDecision,
    RecordingStatus RecordingStatus,
    DateTimeOffset ReviewedAt,
    IReadOnlyList<HistoryReviewDto> Reviews);

public record ReviewerHistoryQuery : PageRequest
{
    public long? TaskId { get; init; }
    public string? Status { get; init; }
    public string? Search { get; init; }
}

/// <summary>Câu tôi đã duyệt nội dung: hành động của tôi + trạng thái câu hiện tại.</summary>
/// <param name="Alignment">Từ tiếng Anh kèm nghĩa tiếng Việt — cùng dạng với lịch sử đóng góp của Speaker.</param>
/// <param name="Reason">Lý do từ chối (mô tả lỗi); null khi lượt của tôi không phải từ chối.</param>
/// <param name="Comment">Bình luận của lượt duyệt, nếu có.</param>
public record ReviewerScriptHistoryItemDto(
    string ScriptId,
    string Kind,
    string Category,
    string CsTranscript,
    string ViEquivalent,
    IReadOnlyList<AlignmentItem> Alignment,
    long SpeakerId,
    string? SpeakerName,
    string MyAction,
    string? Reason,
    string? Comment,
    string ScriptStatus,
    DateTimeOffset ReviewedAt);

public record ReviewerScriptHistoryQuery : PageRequest
{
    public string? Kind { get; init; }
    public string? Status { get; init; }
    public string? Category { get; init; }
    public string? Search { get; init; }
}
