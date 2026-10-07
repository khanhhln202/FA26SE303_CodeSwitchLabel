using CodeSwitchLabel.Services.Dtos;

namespace CodeSwitchLabel.Services.Abstractions;

/// <summary>Trang chủ Speaker ("Đợt"), lịch sử và thống kê của chính mình.</summary>
public interface ISpeakerRoundsService
{
    Task<SpeakerRoundDto?> GetCurrentAsync(long speakerId, CancellationToken ct = default);
    Task<IReadOnlyList<UpcomingRoundDto>> GetUpcomingAsync(long speakerId, CancellationToken ct = default);
    Task<IReadOnlyList<LeaderboardEntryDto>> GetLeaderboardAsync(
        long campaignId, long speakerId, int top, CancellationToken ct = default);
    Task<SpeakerRoundDto> RegisterAsync(long campaignId, long speakerId, CancellationToken ct = default);
    Task UnregisterAsync(long campaignId, long speakerId, CancellationToken ct = default);
    Task<IReadOnlyList<RejectedRecordingDto>> GetRejectedAsync(
        long speakerId, int limit, CancellationToken ct = default);
    Task<PagedResult<SpeakerRecordingHistoryDto>> GetRecordingHistoryAsync(
        long speakerId, SpeakerHistoryQuery query, CancellationToken ct = default);
    Task<PagedResult<SpeakerContributionHistoryDto>> GetContributionHistoryAsync(
        long speakerId, SpeakerContributionHistoryQuery query, CancellationToken ct = default);
    Task<SpeakerStatsDto> GetStatsAsync(long speakerId, CancellationToken ct = default);
}
