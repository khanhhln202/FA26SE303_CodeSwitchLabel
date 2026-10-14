using CodeSwitchLabel.Api.Infrastructure;
using CodeSwitchLabel.Repositories.Enums;
using CodeSwitchLabel.Services.Abstractions;
using CodeSwitchLabel.Services.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CodeSwitchLabel.Api.Controllers;

/// <summary>
/// TEAM_010 — hàng đợi văn bản chung (yêu cầu 2.1).
///
/// Trước đây không tồn tại endpoint nào cho câu chờ duyệt nội dung: màn hình
/// "Review Text" của Speaker thực ra gọi hàng đợi GHI ÂM, còn màn hình duyệt câu của
/// Reviewer chạy trên dữ liệu mô phỏng. Nói cách khác, câu văn bản không có đường
/// đi thật nào để được duyệt.
/// </summary>
[ApiController]
[Tags(ApiTags.Reviews)]
[Authorize]
public class ScriptQueueController(IScriptQueueService queue) : ControllerBase
{
    /// <summary>Lấy câu văn bản chờ duyệt kế tiếp theo thứ tự FIFO.</summary>
    /// <remarks>
    /// Mỗi câu chỉ **một lượt duyệt** (yêu cầu 2.3). Câu được giữ lại cho người gọi
    /// trong khoảng thời gian lấy từ `queue.claim_ttl_minutes`; hết hạn thì người khác
    /// lấy lại được, nên đóng tab không làm kẹt câu.
    ///
    /// Truyền `taskId` thì chỉ nhận câu trong lô của task đó — câu đã được Task
    /// Manager giao cho người khác thì không lấy được.
    ///
    /// Hết câu thì trả `204 No Content`, không phải danh sách rỗng.
    /// </remarks>
    [HttpGet("api/reviewer/scripts/next")]
    [Authorize(Roles = "Reviewer")]
    [ProducesResponseType(typeof(ScriptDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ScriptDetailDto>> NextForTextReview(long? taskId, CancellationToken ct)
    {
        var script = await queue.ClaimNextForTextReviewAsync(User.GetUserId(), taskId, ct);
        return script is null ? NoContent() : Ok(script);
    }

    /// <summary>Số câu đang tồn trong hàng đợi — cho Task Manager quyết định chia lô.</summary>
    /// <remarks>
    /// Yêu cầu 2.2: Task Manager chia duyệt theo TỔNG SỐ CÂU ĐANG TỒN trong hàng đợi
    /// tại thời điểm đó, không gán cố định từng mã câu.
    /// `queued` là câu chưa ai lấy, `claimed` là câu đang bị giữ (kể cả câu hết hạn sắp
    /// được nhặt lại).
    /// </remarks>
    [HttpGet("api/task-manager/text-queue/stats")]
    [Authorize(Roles = "TaskManager,Admin")]
    [ProducesResponseType(typeof(TextQueueStatsDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TextQueueStatsDto>> Stats(CancellationToken ct)
    {
        var text = await queue.GetStatsAsync(QueueStage.TextReview, ct);
        var recording = await queue.GetStatsAsync(QueueStage.Recording, ct);

        return Ok(new TextQueueStatsDto(text.Queued, text.Claimed, recording.Queued, recording.Claimed));
    }

    /// <summary>Chia lô câu chờ duyệt cho một người nhận task (mặc định 100 câu).</summary>
    /// <remarks>
    /// Câu lấy được sẽ gắn `taskId` trong hàng đợi, nên về sau **chỉ người nhận task
    /// đó** lấy được đúng câu đó — không ai trong hàng đợi chung giành được nữa.
    ///
    /// Có thể gọi nhiều lần để chia tiếp cho người khác trong cùng task.
    /// Hết câu thì `claimed` nhỏ hơn `requested`; đó là bình thường, không phải lỗi.
    /// </remarks>
    [HttpPost("api/tasks/{id:long}/text-items")]
    [Authorize(Roles = "TaskManager,Admin")]
    [ProducesResponseType(typeof(AssignTextBatchResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<AssignTextBatchResult>> AssignTextBatch(
        long id, [FromBody] AssignTextBatchRequest request, CancellationToken ct)
        => Ok(await queue.ClaimTextBatchAsync(id, request.UserId, request.Count, ct));
}

/// <summary>
/// Số câu đang tồn trong hàng đợi. Mỗi chặng tách `queued` (chưa ai lấy) và
/// `claimed` (đang có người giữ; hết hạn thì tự quay lại hàng đợi).
/// </summary>
public record TextQueueStatsDto(int TextQueued, int TextClaimed, int RecordingQueued, int RecordingClaimed);

public record AssignTextBatchRequest
{
    /// <summary>Người nhận lô. Phải đang được giao task thì mới chia được.</summary>
    [System.ComponentModel.DataAnnotations.Required]
    public long UserId { get; init; }

    /// <summary>Số câu muốn lấy. Bỏ trống thì dùng <c>text_review.batch_size</c> (mặc định 100).</summary>
    [System.ComponentModel.DataAnnotations.Range(1, 5000)]
    public int? Count { get; init; }
}
