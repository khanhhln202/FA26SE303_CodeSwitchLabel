using CodeSwitchLabel.Services.Dtos;

namespace CodeSwitchLabel.Services.Abstractions;

/// <summary>
/// TEAM_015 — đề xuất câu (yêu cầu 2.5): Speaker thấy một câu ĐÃ duyệt nội dung có vấn đề
/// thì gửi báo lỗi hoặc kèm bản sửa; đề xuất rơi thẳng vào hàng chờ của Reviewer, chờ duyệt.
///
/// Đề xuất chỉ áp dụng cho câu chưa có bản ghi âm nào — đã thu rồi thì sửa nội dung sẽ làm
/// transcript của bản ghi cũ không còn khớp với âm thanh. Đề xuất đang chờ giữ câu khỏi hàng
/// đợi ghi âm; Reviewer chốt xong câu tự quay lại hàng đợi.
///
/// Hai bất biến trên do trigger của database giữ (fn_script_proposal_guard,
/// fn_script_proposal_sync) — service kiểm tra trước để trả thông báo rõ ràng, nhưng
/// chốt cuối luôn nằm trong lược đồ.
/// </summary>
public interface IScriptProposalService
{
    /// <summary>Speaker gửi đề xuất cho một câu đã duyệt nội dung và chưa có bản ghi âm.</summary>
    Task<ProposalCreatedDto> CreateAsync(
        string scriptId, long proposerId, CreateProposalRequest request, CancellationToken ct = default);

    /// <summary>Hàng chờ đề xuất của Reviewer — mặc định cũ nhất trước (chờ lâu nhất xử lý trước).</summary>
    Task<PagedResult<ProposalQueueItemDto>> ListAsync(
        ProposalQueueQuery query, CancellationToken ct = default);

    /// <summary>
    /// Lịch sử đề xuất của chính Speaker — mọi trạng thái, mới nhất trước. Hàng chờ của
    /// Reviewer chỉ có đề xuất đang chờ, nên không dùng được cho màn hình này.
    /// </summary>
    Task<PagedResult<SpeakerProposalHistoryDto>> ListForProposerAsync(
        long proposerId, SpeakerProposalHistoryQuery query, CancellationToken ct = default);

    /// <summary>
    /// Reviewer chốt một đề xuất: duyệt thì ghi nội dung mới vào câu (kèm lượt duyệt sửa
    /// trong lịch sử), từ chối thì giữ nguyên câu. Cả hai đường đều trả câu về hàng đợi ghi âm.
    /// </summary>
    Task<ProposalResolutionDto> ResolveAsync(
        long proposalId, long reviewerId, ResolveProposalRequest request, CancellationToken ct = default);
}
