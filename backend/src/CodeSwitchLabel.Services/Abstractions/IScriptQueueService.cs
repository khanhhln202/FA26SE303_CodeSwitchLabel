using CodeSwitchLabel.Repositories.Entities;
using CodeSwitchLabel.Repositories.Enums;
using CodeSwitchLabel.Repositories.Repositories;
using CodeSwitchLabel.Services.Common;
using CodeSwitchLabel.Services.Dtos;

namespace CodeSwitchLabel.Services.Abstractions;

/// <summary>
/// TEAM_010 — hàng đợi chung cho MỌI câu văn bản (yêu cầu 2.1).
///
/// Một câu đi hai chặng: duyệt văn bản (1 lượt) rồi ghi âm. Hai chặng dùng chung
/// một bảng để thứ tự FIFO và cơ chế giữ chỗ chỉ phải có một bản duy nhất.
///
/// Việc giữ chỗ do hàm database làm (FOR UPDATE SKIP LOCKED), không phải ở đây:
/// "hai người không được nhận cùng một câu" chỉ đúng khi đọc hàng và ghi chủ nằm
/// trong cùng một transaction. Tầng C# chỉ gọi xuống và điều phối quyền.
/// </summary>
public interface IScriptQueueService
{
    /// <summary>Lấy câu chờ duyệt nội dung kế tiếp theo FIFO và giữ lại cho người này.</summary>
    Task<ScriptDetailDto?> ClaimNextForTextReviewAsync(long reviewerId, long? taskId, CancellationToken ct = default);

    /// <summary>Lấy câu chờ ghi âm kế tiếp theo FIFO và giữ lại cho người này.</summary>
    Task<ScriptDetailDto?> ClaimNextForRecordingAsync(long speakerId, long? taskId, CancellationToken ct = default);

    /// <summary>Người này có được phép duyệt câu đang nằm trong lô của mình không.</summary>
    Task<bool> CanReviewAsync(string scriptId, long userId, CancellationToken ct = default);

    /// <summary>
    /// Danh sách câu chờ duyệt nội dung cho màn hình duyệt của Reviewer.
    ///
    /// Khác <see cref="ClaimNextForTextReviewAsync"/>: đó là "đưa tôi câu kế tiếp" và có giữ chỗ,
    /// còn đây chỉ đọc để người duyệt nhìn cả danh sách rồi tự chọn. Đọc không giữ chỗ câu nào.
    /// </summary>
    Task<PagedResult<ContributionReviewItemDto>> ListPendingContributionsAsync(
        long reviewerId, ContributionSearchRequest request, CancellationToken ct = default);

    /// <summary>Chia lô câu chờ duyệt cho một task (yêu cầu 2.2 — mặc định 100 câu/người).</summary>
    Task<AssignTextBatchResult> ClaimTextBatchAsync(
        long taskId, long userId, int? count, CancellationToken ct = default);

    /// <summary>Đóng claim: câu xong việc thì <c>done</c>, trả lại hàng đợi thì <c>queued</c>.</summary>
    Task CompleteAsync(string scriptId, QueueStage stage, CancellationToken ct = default);
    Task ReleaseAsync(string scriptId, QueueStage stage, CancellationToken ct = default);

    /// <summary>
    /// Đưa câu trở lại hàng đợi sau khi bản ghi bị loại. Khác <c>Release</c> ở chỗ chấp
    /// nhận cả hàng đang <c>done</c> — câu đã thu đủ một lần thì hàng của nó là 'done',
    /// chỉ có hàm này mới quay lại được nếu không câu sẽ không bao giờ được thu lại.
    /// </summary>
    Task RequeueAsync(string scriptId, QueueStage stage, CancellationToken ct = default);

    /// <summary>Số câu đang chờ / đang bị giữ, cho màn hình Task Manager.</summary>
    Task<QueueStatsDto> GetStatsAsync(QueueStage stage, CancellationToken ct = default);
}

public record AssignTextBatchResult(long TaskId, int Requested, int Claimed, IReadOnlyList<string> ScriptIds);
