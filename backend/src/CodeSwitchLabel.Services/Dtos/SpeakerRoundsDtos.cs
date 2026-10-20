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
/// <param name="Submitted">Số bản đã nộp trong đợt — số thật, dùng để xếp hạng.</param>
/// <param name="TargetQty">
/// Chỉ tiêu riêng của người này trong đợt: tổng chỉ tiêu các nhiệm vụ thu âm họ đang nhận.
/// Task nhiều người nhận thì mỗi người lấy trọn chỉ tiêu của task — đó là mục tiêu của
/// nhiệm vụ, không chia nhỏ theo đầu người. 0 nghĩa là chưa nhận nhiệm vụ nào trong đợt.
/// </param>
/// <param name="SubmittedCapped">
/// <paramref name="Submitted"/> nhưng không vượt <paramref name="TargetQty"/> — dùng vẽ thanh
/// tiến độ. Hệ thống cho thu tiếp sau khi đạt chỉ tiêu nên số thật vượt mốc là bình thường;
/// xếp hạng vẫn tính trên số thật để người làm nhiều hơn không bị tụt hạng oan.
/// </param>
public record LeaderboardEntryDto(
    int Rank, long UserId, string Name, int Submitted, bool IsMe, int TargetQty, int SubmittedCapped);

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
/// <param name="Reviews">
/// Mọi lượt duyệt của CẢ HAI bản, kể cả khi bản ghi chưa chốt — mỗi lượt kèm
/// <see cref="HistoryReviewDto.RecordingId"/> để biết nó thuộc bản nào.
/// </param>
/// <param name="ReviewedRounds">
/// Số lượt duyệt đã có của bản ÍT lượt nhất trong cặp. Cặp chỉ xong khi cả hai bản xong,
/// nên đây mới là tiến độ thật của cặp — hiện "ReviewedRounds/RoundsRequired".
/// </param>
/// <param name="RoundsRequired">Số lượt duyệt cần có cho mỗi bản ghi (system_config).</param>
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
    IReadOnlyList<HistoryReviewDto> Reviews,
    int ReviewedRounds,
    int RoundsRequired);

/// <param name="Reason">
/// Bình luận của người duyệt; lượt từ chối không kèm bình luận thì là mô tả các lý do đã chọn.
/// </param>
/// <param name="RecordingId">
/// Bản ghi mà lượt duyệt này thuộc về. Null ở lịch sử của Reviewer (ở đó mỗi dòng đã là
/// một bản ghi rồi), có giá trị ở lịch sử của Speaker vì một dòng là một CẶP hai bản.
/// </param>
public record HistoryReviewDto(
    int Round,
    string Decision,
    string? Reason,
    string? RecordingId = null,
    DateTimeOffset? ReviewedAt = null);

/// <summary>
/// Số liệu của một nhóm việc của Speaker. Cả hai nhóm đều đếm bản ghi / câu, không đếm theo cặp.
/// </summary>
public record SpeakerStatsDto(RecordingStats Recording, ContributionStats Contribution);

/// <param name="Pending">Đã nộp nhưng chưa có kết quả duyệt, cộng cả bản trượt QC.</param>
public record RecordingStats(int Total, int Approved, int Rejected, int Pending);

/// <param name="Validated">Câu đã qua bước duyệt nội dung và có thể giao đi thu âm.</param>
/// <param name="Rejected">Câu bị từ chối, cộng cả câu đã bị ẩn.</param>
public record ContributionStats(int Total, int Validated, int Rejected, int Pending);

/// <summary>
/// Lượt duyệt nội dung của một câu đóng góp.
///
/// Duyệt văn bản chỉ có MỘT lượt (TEAM_010), khác bản ghi âm cần ba lượt quá bán — nên ở đây
/// là một đối tượng chứ không phải danh sách, và không có số vòng.
/// </summary>
public record ContributionReviewDto(
    long ReviewId,
    string Action,
    long ReviewerId,
    string ReviewerName,
    string? ErrorReasonCode,
    string? ErrorReasonDescription,
    string? Comment,
    DateTimeOffset ReviewedAt);

/// <param name="Review">Null khi câu còn đang chờ duyệt.</param>
public record SpeakerContributionHistoryDto(
    string ScriptId,
    string Category,
    string CsTranscript,
    string ViEquivalent,
    IReadOnlyList<AlignmentItem> Alignment,
    DateTimeOffset CreatedAt,
    ScriptStatus Status,
    ContributionReviewDto? Review);

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
