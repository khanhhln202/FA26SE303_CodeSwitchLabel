using CodeSwitchLabel.Repositories.Enums;

namespace CodeSwitchLabel.Services.Dtos;

/// <summary>Một lượt duyệt của bản ghi mới nhất trong cặp, kèm tên người duyệt để Admin khỏi phải ghép thêm.</summary>
public record AdminReviewDto(
    int Round,
    string Decision,
    string? Reason,
    string RecordingId,
    long ReviewerId,
    string ReviewerName,
    DateTimeOffset ReviewedAt);

/// <summary>
/// Một CẶP bản ghi của một Speaker, gom từ hai biến thể. Trạng thái của cặp do BE tính bằng
/// <see cref="CodeSwitchLabel.Services.Reviews.ReviewRules.PairOutcome"/> — cùng luật với lịch sử của Reviewer và Speaker.
/// </summary>
public record AdminRecordingPairDto(
    string Id,
    IReadOnlyList<string> RawIds,
    string ScriptId,
    long SpeakerId,
    string Speaker,
    string SpeakerEmail,
    string CsText,
    string ViText,
    string Topic,
    long? TaskId,
    string? TaskTitle,
    string? CsRecordingId,
    string? ViRecordingId,
    string? CsAudioUrl,
    string? ViAudioUrl,
    decimal DurationSec,
    decimal TotalDurationSec,
    RecordingStatus Status,
    RecordingStatus? CsStatus,
    RecordingStatus? ViStatus,
    IReadOnlyList<AdminReviewDto> Reviews,
    DateTimeOffset CreatedAt);
