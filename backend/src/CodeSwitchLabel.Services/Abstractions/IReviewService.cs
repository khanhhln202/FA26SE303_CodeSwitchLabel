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
    /// Danh sách bản ghi trong một task duyệt đang giao cho người này. Không kèm quyết định của
    /// bất kỳ ai, chỉ kèm số lượt đã có và cờ "mình đã duyệt chưa".
    /// </summary>
    Task<PagedResult<TaskReviewItemDto>> GetTaskRecordingsAsync(
        long taskId, long reviewerId, TaskReviewQuery query, CancellationToken ct = default);

    /// <summary>
    /// Mở một bản ghi cụ thể để duyệt, thay vì nhận bản do hệ thống chọn. Cùng bộ điều kiện với
    /// <see cref="GetNextAsync"/>; không đủ điều kiện thì ném lỗi nói rõ vì sao.
    /// </summary>
    Task<NextReviewDto> GetForReviewAsync(
        string recordingId, long reviewerId, CancellationToken ct = default);

    /// <summary>
    /// Ghi một lượt duyệt độc lập. Đủ số vòng yêu cầu thì trigger của database chốt trạng thái
    /// bản ghi theo đa số — tầng này chỉ đọc lại kết quả.
    /// </summary>
    Task<SubmitReviewResult> SubmitAsync(
        string recordingId, long reviewerId, SubmitReviewRequest request, CancellationToken ct = default);

    /// <summary>
    /// TEAM_010 (yêu cầu 2.4) — nút CHỈ XÓA AUDIO: bản ghi bị loại nhưng CÂU VĂN BẢN
    /// được giữ lại và quay về hàng đợi ghi âm để thu lại.
    ///
    /// Quay lại cho CHÍNH người đọc cũ, không phải người khác: luật của lược đồ là một
    /// cặp câu chỉ một người đọc (trg_recording_single_speaker), và đổi người đọc giữa
    /// hai bản sẽ phá vỡ tính nhất quán giọng của cặp.
    /// </summary>
    Task<RejectAudioResult> RejectAudioAsync(
        string recordingId, long reviewerId, RejectAudioRequest request, CancellationToken ct = default);

    /// <summary>
    /// TEAM_010 (yêu cầu 2.4) — nút XÓA HOÀN TOÀN: cả câu văn bản lẫn bản ghi đều bị
    /// loại khỏi kho (xoá mềm: script.status = 'deactivated').
    ///
    /// Dành cho câu vô nghĩa hoặc lỗi nghiêm trọng — loại vĩnh viễn để không quay lại
    /// hàng đợi và gây vòng lặp vô tận.
    /// </summary>
    Task<DiscardPairResult> DiscardPairAsync(
        string recordingId, long reviewerId, DiscardPairRequest request, CancellationToken ct = default);

    Task<IReadOnlyList<ReviewDto>> GetHistoryAsync(
        string recordingId, long viewerId, ViewerRole role, CancellationToken ct = default);

    Task<ReviewerProgressDto> GetProgressAsync(long reviewerId, CancellationToken ct = default);
}
