using CodeSwitchLabel.Services.Dtos;

namespace CodeSwitchLabel.Services.Abstractions;

/// <summary>Ai đang xem lịch sử duyệt — quyết định thấy được tới đâu.</summary>
public enum ViewerRole
{
    Speaker,
    Reviewer,
    Manager
}

public interface IReviewService
{
    Task<NextReviewDto?> GetNextAsync(
        long reviewerId, long? taskId, long? speakerId, bool random, CancellationToken ct = default);

    /// <summary>
    /// Ghi một lượt duyệt độc lập. Đủ số vòng yêu cầu thì trigger của database chốt trạng thái
    /// bản ghi theo đa số — tầng này chỉ đọc lại kết quả.
    /// </summary>
    Task<SubmitReviewResult> SubmitAsync(
        string recordingId, long reviewerId, SubmitReviewRequest request, CancellationToken ct = default);

    Task<IReadOnlyList<ReviewDto>> GetHistoryAsync(
        string recordingId, long viewerId, ViewerRole role, CancellationToken ct = default);

    Task<ReviewerProgressDto> GetProgressAsync(long reviewerId, CancellationToken ct = default);
}
