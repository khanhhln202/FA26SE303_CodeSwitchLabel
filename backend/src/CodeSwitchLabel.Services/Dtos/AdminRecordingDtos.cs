using CodeSwitchLabel.Repositories.Enums;

namespace CodeSwitchLabel.Services.Dtos;

/// <summary>
/// Một lượt duyệt, kèm tên người duyệt để Admin khỏi phải ghép thêm.
///
/// <b><see cref="RecordingId"/> là trường bắt buộc phải dùng khi hiển thị.</b> Danh sách
/// <c>Reviews</c> của một cặp gộp phiếu của <b>CẢ HAI biến thể</b> (cs và vi), nên giao diện
/// phải nhóm theo <c>RecordingId</c> rồi mới vẽ. Đổ chung vào một ô ba vòng sẽ khiến phiếu của
/// bản vi trông như phiếu của bản cs vừa thu xong — một cặp có thể có bản vi đã đủ phiếu trong
/// khi bản cs mới thu lại và chưa có phiếu nào.
/// </summary>
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
