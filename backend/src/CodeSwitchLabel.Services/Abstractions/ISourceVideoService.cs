using CodeSwitchLabel.Repositories.Enums;
using CodeSwitchLabel.Services.Dtos;

namespace CodeSwitchLabel.Services.Abstractions;

/// <summary>
/// Corpus hội thoại lấy từ YouTube: nhận link, chạy nền tải + phiên âm, Reviewer chốt câu,
/// Admin tải ZIP. Không đi qua bảng recording vì dữ liệu là đoạn hội thoại dài, không phải cặp câu.
/// </summary>
public interface ISourceVideoService
{
    Task<SourceVideoDto> SubmitAsync(
        SubmitSourceVideoRequest request, long userId, CancellationToken ct = default);

    Task<SourceVideoDto> GetAsync(
        long sourceVideoId, long userId, bool isAdmin, CancellationToken ct = default);

    Task<PagedResult<SourceVideoListItem>> ListAsync(
        PageRequest page, SourceVideoStatus? status, CancellationToken ct = default);

    /// <summary>Reviewer nhận video kế tiếp; null khi hàng đợi rỗng.</summary>
    Task<SourceVideoDto?> ClaimNextAsync(long reviewerId, CancellationToken ct = default);

    Task<SourceVideoDto> SaveSegmentsAsync(
        long sourceVideoId, long reviewerId, SaveSourceSegmentsRequest request,
        CancellationToken ct = default);

    /// <summary>Cắt clip WAV cho từng câu, đẩy lên kho, rồi mới đánh dấu duyệt xong.</summary>
    Task<SourceVideoDto> ApproveAsync(
        long sourceVideoId, long reviewerId, CancellationToken ct = default);

    /// <summary>Đưa job Failed về hàng đợi. Job đang chạy không bị đụng.</summary>
    Task RetryAsync(long sourceVideoId, CancellationToken ct = default);

    Task<DatasetFileDto> ExportAsync(CancellationToken ct = default);

    /// <summary>Một lượt của tiến trình nền. Trả false khi không còn video nào cần xử lý.</summary>
    Task<bool> ProcessNextAsync(CancellationToken ct = default);
}
