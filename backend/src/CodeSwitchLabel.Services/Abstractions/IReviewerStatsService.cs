using CodeSwitchLabel.Services.Dtos;

namespace CodeSwitchLabel.Services.Abstractions;

/// <summary>Thống kê và lịch sử duyệt của chính Reviewer (chỉ từ lượt duyệt của mình).</summary>
public interface IReviewerStatsService
{
    Task<ReviewerTotalsDto> GetTotalsAsync(long reviewerId, CancellationToken ct = default);
    Task<IReadOnlyList<RejectReasonStatDto>> GetRejectReasonsAsync(
        long reviewerId, CancellationToken ct = default);
    Task<IReadOnlyList<TopRejectedSentenceDto>> GetTopRejectedSentencesAsync(
        long reviewerId, int limit, CancellationToken ct = default);
    Task<IReadOnlyList<TopRejectedSpeakerDto>> GetTopRejectedSpeakersAsync(
        long reviewerId, int limit, CancellationToken ct = default);
    Task<PagedResult<ReviewerHistoryItemDto>> GetHistoryAsync(
        long reviewerId, ReviewerHistoryQuery query, CancellationToken ct = default);
    Task<PagedResult<ReviewerScriptHistoryItemDto>> GetScriptHistoryAsync(
        long reviewerId, ReviewerScriptHistoryQuery query, CancellationToken ct = default);
}
