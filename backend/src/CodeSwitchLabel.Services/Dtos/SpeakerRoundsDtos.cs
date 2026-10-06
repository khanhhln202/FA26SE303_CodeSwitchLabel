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

public record LeaderboardEntryDto(int Rank, long UserId, string Name, int Approved, bool IsMe);

/// <summary>Bản ghi bị từ chối của chính mình, kèm lý do và link nghe lại để thu lại.</summary>
public record RejectedRecordingDto(
    string RecordingId,
    string ScriptId,
    string Transcript,
    string? Reason,
    string AudioUrl,
    DateTimeOffset AudioUrlExpiresAt);

/// <summary>Dòng lịch sử ghi âm của Speaker: cả hai biến thể gộp theo cặp câu ghi.</summary>
public record SpeakerRecordingHistoryDto(
    string RecordingId,
    string ScriptId,
    string? TaskTitle,
    string CsText,
    string ViText,
    string? CsAudioUrl,
    string? ViAudioUrl,
    decimal DurationSec,
    RecordingStatus Status,
    DateTimeOffset RecordedAt,
    IReadOnlyList<HistoryReviewDto> Reviews);

public record HistoryReviewDto(int Round, string Decision, string? Reason);

public record SpeakerStatsDto(int Total, int Approved, int Rejected, int Pending);

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
