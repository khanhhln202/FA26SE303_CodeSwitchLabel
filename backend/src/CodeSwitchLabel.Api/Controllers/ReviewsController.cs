using System.Security.Claims;
using CodeSwitchLabel.Api.Infrastructure;
using CodeSwitchLabel.Services.Abstractions;
using CodeSwitchLabel.Services.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CodeSwitchLabel.Api.Controllers;

[ApiController]
[Tags(ApiTags.Reviews)]
[Authorize]
public class ReviewsController(IReviewService reviewService) : ControllerBase
{
    /// <summary>Lấy bản ghi tiếp theo cần duyệt.</summary>
    /// <remarks>
    /// Mỗi bản ghi cần **ba lượt duyệt độc lập của ba người khác nhau**, và **mọi lượt đều duyệt mù**:
    /// bạn không thấy ý kiến của ai khác. Đủ ba lượt thì database chốt theo đa số.
    ///
    /// Kèm sẵn câu cần đối chiếu (`scriptText` đã bỏ nhãn, `scriptTagged` còn nhãn để tô màu),
    /// thời lượng và link nghe tạm.
    ///
    /// Lọc theo `speakerId` để duyệt theo từng người đọc, hoặc `random=true` để lấy ngẫu nhiên.
    /// Truyền `taskId` thì chỉ nhận bản đang chờ trong task đó.
    ///
    /// Endpoint này **không giữ chỗ** — hai Reviewer có thể nhận cùng một bản. Người bấm duyệt sau
    /// nhận 409 và chỉ việc gọi lại để lấy bản khác.
    /// </remarks>
    [HttpGet("api/reviewer/recordings/next")]
    [Authorize(Roles = "Reviewer")]
    [ProducesResponseType(typeof(NextReviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<NextReviewDto>> Next(
        [FromQuery] long? taskId,
        [FromQuery] long? speakerId,
        [FromQuery] bool random = false,
        CancellationToken ct = default)
    {
        var next = await reviewService.GetNextAsync(User.GetUserId(), taskId, speakerId, random, ct);
        return next is null ? NoContent() : Ok(next);
    }

    /// <summary>Danh sách bản ghi trong một task duyệt được giao cho mình.</summary>
    /// <remarks>
    /// Dùng cho màn hình "task duyệt của tôi": thấy cả task một lượt thay vì bấm `next` từng bản.
    ///
    /// **Giữ nguyên luật duyệt mù** — không trả quyết định của ai. Mỗi bản chỉ cho biết đã có mấy
    /// lượt trên tổng số cần có (`reviewsDone`/`roundsRequired`), và bản thân bạn đã duyệt chưa
    /// (`myReviewDone`). Biết "2/3 lượt" không cho biết hai lượt kia đạt hay trượt.
    ///
    /// `canReview` cho biết bấm vào duyệt được ngay hay không, `blocker` nói vì sao không:
    /// `OwnRecording`, `AlreadyReviewedByMe`, `NotPendingReview`, `RoundsFull`.
    ///
    /// Mặc định trả cả task; đặt `onlyReviewable=true` để chỉ còn những bản duyệt được.
    /// Bấm vào một bản thì gọi `GET /api/reviewer/recordings/{id}` để lấy câu và link nghe.
    /// </remarks>
    [HttpGet("api/reviewer/tasks/{taskId:long}/recordings")]
    [Authorize(Roles = "Reviewer")]
    [ProducesResponseType(typeof(PagedResult<TaskReviewItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResult<TaskReviewItemDto>>> TaskRecordings(
        long taskId, [FromQuery] TaskReviewQuery query, CancellationToken ct)
        => Ok(await reviewService.GetTaskRecordingsAsync(taskId, User.GetUserId(), query, ct));

    /// <summary>Mở một bản ghi cụ thể để duyệt.</summary>
    /// <remarks>
    /// Trả đúng dữ liệu như `GET /api/reviewer/recordings/next`, nhưng cho bản **bạn tự chọn**
    /// từ danh sách của task. Điều kiện cũng y như vậy, và lỗi trả về mã giống lúc nộp duyệt:
    ///
    /// - **403 `self_review_forbidden`** — bản ghi do chính bạn thu.
    /// - **409 `already_reviewed`** — bạn đã duyệt bản này.
    /// - **409 `recording_not_reviewable`** — bản ghi không còn chờ duyệt.
    /// - **409 `no_more_rounds`** — đã đủ số lượt.
    ///
    /// Đường `next` là một segment cố định nên không đụng vào `{id}` ở đây.
    /// </remarks>
    [HttpGet("api/reviewer/recordings/{id}")]
    [Authorize(Roles = "Reviewer")]
    [ProducesResponseType(typeof(NextReviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<NextReviewDto>> ForReview(string id, CancellationToken ct)
        => Ok(await reviewService.GetForReviewAsync(id, User.GetUserId(), ct));

    /// <summary>Duyệt đạt hoặc từ chối một bản ghi.</summary>
    /// <remarks>
    /// Từ chối thì **bắt buộc ít nhất một** mã lý do lấy từ `GET /api/rejection-reasons`;
    /// duyệt đạt thì không được kèm lý do.
    ///
    /// Không được duyệt bản ghi do chính mình thu, và mỗi người chỉ duyệt một bản **một lần**.
    ///
    /// **expectedRound là bắt buộc** — chép nguyên trường `round` nhận được từ GET next.
    /// Nếu trong lúc bạn đang nghe có người khác duyệt xong vòng đó, server trả **409 round_changed**.
    ///
    /// Kết quả trả về cho biết đã đủ ba lượt chưa (`isFinal`) và trạng thái bản ghi sau khi
    /// database chốt theo đa số.
    /// </remarks>
    [HttpPost("api/recordings/{id}/reviews")]
    [Authorize(Roles = "Reviewer")]
    [ProducesResponseType(typeof(SubmitReviewResult), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<SubmitReviewResult>> Submit(
        string id, [FromBody] SubmitReviewRequest request, CancellationToken ct)
    {
        var result = await reviewService.SubmitAsync(id, User.GetUserId(), request, ct);
        return Created($"/api/recordings/{id}/reviews", result);
    }

    /// <summary>Lịch sử duyệt của một bản ghi.</summary>
    /// <remarks>
    /// Mỗi vai thấy tới đâu:
    ///
    /// - **Admin, Task Manager** — thấy toàn bộ.
    /// - **Reviewer** — bản đã chốt thì thấy toàn bộ; bản còn đang duyệt thì chỉ thấy lượt của chính mình,
    ///   để không ai xem trộm ý kiến người khác trước khi tự quyết định.
    /// - **Speaker** — chỉ bản của mình, chỉ sau khi đã chốt, và không thấy ai là người duyệt.
    /// </remarks>
    [HttpGet("api/recordings/{id}/reviews")]
    [ProducesResponseType(typeof(IReadOnlyList<ReviewDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<ReviewDto>>> History(string id, CancellationToken ct)
        => Ok(await reviewService.GetHistoryAsync(id, User.GetUserId(), ViewerRoleOf(User), ct));

    /// <summary>Tiến độ duyệt của chính mình.</summary>
    /// <remarks>Số đã duyệt, chỉ tiêu, phần trăm và thời gian còn lại cho từng task đang giao.</remarks>
    [HttpGet("api/reviewer/progress")]
    [Authorize(Roles = "Reviewer")]
    [ProducesResponseType(typeof(ReviewerProgressDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ReviewerProgressDto>> Progress(CancellationToken ct)
        => Ok(await reviewService.GetProgressAsync(User.GetUserId(), ct));

    private static ViewerRole ViewerRoleOf(ClaimsPrincipal user) =>
        user.IsInRole("Admin") || user.IsInRole("TaskManager") ? ViewerRole.Manager
        : user.IsInRole("Reviewer") ? ViewerRole.Reviewer
        : ViewerRole.Speaker;
}
