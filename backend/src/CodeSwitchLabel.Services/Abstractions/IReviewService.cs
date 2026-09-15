using CodeSwitchLabel.Services.Dtos;

namespace CodeSwitchLabel.Services.Abstractions;

/// <summary>Người xem lịch sử duyệt thuộc nhóm nào — quyết định được thấy tới đâu.</summary>
public enum ViewerRole
{
    Speaker,
    Reviewer,

    /// <summary>Task Manager hoặc Admin — thấy toàn bộ.</summary>
    Manager
}

public interface IReviewService
{
    /// <summary>Bản ghi tiếp theo cần duyệt. Null khi hết — trạng thái bình thường, không phải lỗi.</summary>
    Task<NextReviewDto?> GetNextAsync(
        long reviewerId, long? taskId, long? speakerId, bool random, CancellationToken ct = default);

    Task<SubmitReviewResult> SubmitAsync(
        long recordingId, long reviewerId, SubmitReviewRequest request, CancellationToken ct = default);

    Task<IReadOnlyList<ReviewDto>> GetHistoryAsync(
        long recordingId, long viewerId, ViewerRole role, CancellationToken ct = default);

    Task<ReviewerProgressDto> GetProgressAsync(long reviewerId, CancellationToken ct = default);
}
