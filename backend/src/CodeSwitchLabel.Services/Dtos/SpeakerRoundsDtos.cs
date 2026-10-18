using CodeSwitchLabel.Repositories.Enums;

namespace CodeSwitchLabel.Services.Dtos;

/// <summary>Một "Đợt" ở FE = một chiến dịch ở BE, kèm tiến độ của chính người đọc.</summary>
public record SpeakerRoundDto(
    long CampaignId,
    string Name,
    string? Topic,
    CampaignStatus Status,
    DateOnly StartDate,
    DateOnly EndDate,
    string? EndsIn,
    int Participants,
    int Target,
    int Submitted,
    int Approved,
    int Pending,
    bool IsRegistered);

public record UpcomingRoundDto(
    long CampaignId,
    string Name,
    string? Topic,
    int Target,
    string Period,
    string? RegisterEndsIn,
    string? RegisterOpensAt,
    bool IsRegistered);

/// <summary>
/// Một dòng bảng xếp hạng của đợt. TEAM_016: <see cref="Submitted"/> là số bản ghi
/// người này ĐÃ NỘP (chờ duyệt / đạt / bị từ chối), không phải chỉ bản được duyệt.
/// </summary>
public record LeaderboardEntryDto(int Rank, long UserId, string Name, int Submitted, bool IsMe);

/// <summary>
/// Bảng xếp hạng kèm mốc để vẽ thanh tiến độ.
///
/// Hai mốc vì chúng khác nhau rất xa và dùng cho hai câu hỏi khác nhau:
/// <see cref="CampaignTargetQty"/> là chỉ tiêu cả đợt (còn bao xa tới đích),
/// <see cref="AllocatedTargetQty"/> là tổng chỉ tiêu các nhiệm vụ đã chia (còn bao nhiêu
/// việc đang thật sự giao). Đợt mới mở thường có mốc thứ hai nhỏ hơn mốc thứ nhất rất nhiều.
/// </summary>
public record LeaderboardDto(
    int CampaignTargetQty,
    int AllocatedTargetQty,
    IReadOnlyList<LeaderboardEntryDto> Items);

/// <summary>Bản ghi bị từ chối của chính mình, kèm lý do và link nghe lại để thu lại.</summary>
public record RejectedRecordingDto(
    string RecordingId,
    string ScriptId,
    string Transcript,
    string? Reason,
    string AudioUrl,
    DateTimeOffset AudioUrlExpiresAt);

/// <summary>Dòng lịch sử ghi âm của Speaker: mỗi dòng là MỘT CẶP bản ghi (Việt-Anh + tiếng Việt).</summary>
/// <param name="CsRecordingId">Null = cặp mới chỉ ghi được một bản.</param>
/// <param name="CsAudioUrl">Link nghe tạm, hết hạn sau 15 phút.</param>
/// <param name="DurationSec">Bản Việt-Anh, hoặc bản tiếng Việt nếu cặp chỉ có một bản.</param>
/// <param name="Status">Trạng thái của CẶP, gộp từ hai bản.</param>
/// <param name="RecordedAt">Mốc của bản ghi mới nhất trong cặp.</param>
public record SpeakerRecordingHistoryDto(
    string ScriptId,
    string? TaskTitle,
    string CsText,
    string ViText,
    string? CsRecordingId,
    string? ViRecordingId,
    string? CsAudioUrl,
    string? ViAudioUrl,
    decimal DurationSec,
    RecordingStatus Status,
    DateTimeOffset RecordedAt,
    IReadOnlyList<HistoryReviewDto> Reviews);

public record HistoryReviewDto(int Round, string Decision, string? Reason);

/// <summary>
/// Số liệu của một nhóm việc của Speaker. Cả hai nhóm đều đếm bản ghi / câu, không đếm theo cặp.
/// </summary>
public record SpeakerStatsDto(RecordingStats Recording, ContributionStats Contribution);

/// <param name="Pending">Đã nộp nhưng chưa có kết quả duyệt, cộng cả bản trượt QC.</param>
public record RecordingStats(int Total, int Approved, int Rejected, int Pending);

/// <param name="Validated">Câu đã qua bước duyệt nội dung và có thể giao đi thu âm.</param>
/// <param name="Rejected">Câu bị từ chối, cộng cả câu đã bị ẩn.</param>
public record ContributionStats(int Total, int Validated, int Rejected, int Pending);

/// <summary>Dòng lịch sử đóng góp câu của Speaker.</summary>
public record SpeakerContributionHistoryDto(
    string ScriptId,
    string Category,
    string CsTranscript,
    string ViEquivalent,
    IReadOnlyList<AlignmentItem> Alignment,
    DateTimeOffset CreatedAt,
    ScriptStatus Status,
    IReadOnlyList<HistoryReviewDto> Reviews);

public record SpeakerHistoryQuery : PageRequest
{
    public RecordingStatus? Status { get; init; }
    public long? TaskId { get; init; }
    public string? Search { get; init; }
}

public record SpeakerContributionHistoryQuery : PageRequest
{
    public string? Category { get; init; }
    public string? Search { get; init; }
}
