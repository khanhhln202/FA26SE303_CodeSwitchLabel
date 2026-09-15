using System.Security.Claims;
using CodeSwitchLabel.Api.Infrastructure;
using CodeSwitchLabel.Services.Abstractions;
using CodeSwitchLabel.Services.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CodeSwitchLabel.Api.Controllers;

[ApiController]
[Tags("7 · Duyệt bản ghi")]
[Authorize]
public class ReviewsController(IReviewService reviewService) : ControllerBase
{
    /// <summary>Lấy bản ghi tiếp theo cần duyệt.</summary>
    /// <remarks>
    /// Kèm sẵn nội dung script, link nghe tạm thời, và cho biết đây là vòng mấy:
    ///
    /// - **Primary** (vòng 1) — quyết định chính.
    /// - **SpotCheck** (vòng 2) — bản được rút ngẫu nhiên để kiểm tra lại. **Duyệt mù**: không thấy gì của vòng 1.
    /// - **Adjudication** (vòng 3) — hai vòng trước lệch nhau. Thấy cả hai ý kiến trong previousReviews.
    ///
    /// Lọc theo speakerId để duyệt theo từng người đọc, hoặc random=true để lấy ngẫu nhiên.
    /// Truyền taskId thì chỉ nhận bản vòng 1 nằm trong task đó.
    ///
    /// Endpoint này **không giữ chỗ** — hai Reviewer có thể nhận cùng một bản.
    /// Người bấm duyệt sau sẽ nhận 409; giao diện chỉ việc gọi lại endpoint này để lấy bản khác.
    ///
    /// **204 No Content** nghĩa là hết bản cần duyệt.
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

    /// <summary>Duyệt đạt hoặc từ chối một bản ghi.</summary>
    /// <remarks>
    /// Từ chối thì **bắt buộc ít nhất một** mã lý do lấy từ <c>GET /api/rejection-reasons</c> —
    /// một bản ghi có thể vừa ồn vừa đọc sai. Duyệt đạt thì không được kèm lý do.
    ///
    /// Kết quả trả về cho biết bản ghi đã chốt hay còn chờ vòng sau (isFinal).
    /// Ở vòng 1, một phần bản ghi được rút ngẫu nhiên theo tham số review.random_ratio để
    /// kiểm tra mù ở vòng 2 — những bản đó vẫn ở trạng thái chờ duyệt cho tới khi chốt.
    ///
    /// Không được duyệt bản ghi do chính mình thu, và mỗi vòng phải là một người khác.
    ///
    /// **expectedRound là bắt buộc** — chép nguyên trường round nhận được từ GET next.
    /// Nếu trong lúc bạn đang nghe, người khác đã duyệt xong vòng đó, server trả **409 round_changed**
    /// thay vì âm thầm ghi quyết định của bạn vào vòng sau.
    /// </remarks>
    [HttpPost("api/recordings/{id:long}/reviews")]
    [Authorize(Roles = "Reviewer")]
    [ProducesResponseType(typeof(SubmitReviewResult), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<SubmitReviewResult>> Submit(
        long id, [FromBody] SubmitReviewRequest request, CancellationToken ct)
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
    ///   để không ai xem trộm vòng 1 trước khi làm vòng kiểm tra mù.
    /// - **Speaker** — chỉ bản của mình, chỉ sau khi đã chốt, và không thấy ai là người duyệt.
    /// </remarks>
    [HttpGet("api/recordings/{id:long}/reviews")]
    [ProducesResponseType(typeof(IReadOnlyList<ReviewDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<ReviewDto>>> History(long id, CancellationToken ct)
        => Ok(await reviewService.GetHistoryAsync(id, User.GetUserId(), ViewerRoleOf(User), ct));

    /// <summary>Tiến độ duyệt của chính mình.</summary>
    /// <remarks>
    /// Đúng use case "Track Review Progress" của đề tài: số đã duyệt, chỉ tiêu, phần trăm,
    /// và thời gian còn lại tới hạn cho từng task đang giao. Backend tính sẵn mọi con số.
    /// </remarks>
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
