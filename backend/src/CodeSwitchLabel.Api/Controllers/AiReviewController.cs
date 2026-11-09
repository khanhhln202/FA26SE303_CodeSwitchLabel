using System.ComponentModel.DataAnnotations;
using CodeSwitchLabel.Api.Infrastructure;
using CodeSwitchLabel.Services.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CodeSwitchLabel.Api.Controllers;

[ApiController]
[Tags(ApiTags.AiReview)]
[Authorize]
public class AiReviewController(IAiReviewService aiReview) : ControllerBase
{
    /// <summary>Cho máy bỏ phiếu thứ ba để phá thế hai người duyệt lệch nhau.</summary>
    /// <remarks>
    /// **Máy KHÔNG phải nút bấm lúc nào cũng gọi được.** Luật: hai Reviewer người duyệt trước.
    /// Cùng kết quả thì bản ghi chốt ngay ở lượt 2 và máy không tham gia. Chỉ khi hai người
    /// **lệch nhau** (1 đạt / 1 không đạt) thì máy mới vào làm **phiếu thứ ba**.
    ///
    /// Chưa tới lượt máy thì trả `409 ai_review_not_tiebreak`, và **không gọi ASR** — nên không
    /// tốn tiền cho bản chưa tới lượt.
    ///
    /// Khi đã tới lượt: máy lấy audio từ kho, gọi ASR chuyển thành chữ, so với văn bản đáng ra
    /// phải đọc và tính **tỉ lệ sai từ (WER)**. Tính hai lần — giữ dấu tiếng Việt và bỏ dấu —
    /// rồi phán theo con số nhỏ hơn, vì ASR mất dấu là giới hạn của mô hình chứ không phải lỗi
    /// người đọc.
    ///
    /// Hai kết quả, không có kết quả thứ ba:
    /// - `rejected` — WER từ `ai_review.wer_decision_threshold` trở lên, lý do `content_mismatch`.
    /// - `approved` — WER dưới ranh giới đó.
    ///
    /// Là người phá thế nên máy **luôn phải chọn một bên**: bỏ quyền sẽ để lại bản ghi 1–1 không
    /// ai quyết. Máy chỉ nêu được `content_mismatch`; `background_noise`, `mispronunciation` và
    /// `other` nó không đo được nên không bao giờ tự chọn.
    ///
    /// Phiếu của máy đi qua đúng đường mà người dùng đi, nên vẫn chịu mọi ràng buộc: không duyệt
    /// hai lần, không duyệt bản mình thu. Và vì là lượt thứ ba, **trigger đa số của database chốt
    /// bản ghi ngay** sau phiếu này.
    /// </remarks>
    /// <response code="422">Tính năng đang tắt, chưa cấu hình ASR, hoặc ASR gọi không được.</response>
    /// <response code="409">Chưa tới lượt máy (`ai_review_not_tiebreak`), bản ghi không còn chờ duyệt, hoặc máy đã duyệt rồi.</response>
    [HttpPost("api/recordings/{id}/ai-review")]
    [Authorize(Roles = "TaskManager,Admin")]
    [ProducesResponseType(typeof(AiReviewResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<AiReviewResultDto>> Analyze(string id, CancellationToken ct) =>
        Ok(await aiReview.AnalyzeAsync(id, ct));

    /// <summary>Cho máy phá thế một lô bản ghi đang bị hai người duyệt lệch nhau.</summary>
    /// <remarks>
    /// Chỉ lấy những bản còn `pending_review` **đang ở thế 1 đạt / 1 không đạt** mà máy chưa
    /// từng phán, cũ nhất trước. Bản chưa tới lượt máy không nằm trong lô.
    ///
    /// `limit` là **trần bắt buộc**, không phải gợi ý: mỗi bản là một lần gọi ASR có tính phí.
    ///
    /// Một bản lỗi không làm đổ cả lô — nó vào danh sách `skipped` kèm mã lỗi, và vì không ghi
    /// dòng bằng chứng nào nên lượt chạy sau vẫn thử lại được.
    /// </remarks>
    [HttpPost("api/ai-review/run")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(AiReviewBatchResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<AiReviewBatchResultDto>> Run(
        [FromQuery][Range(1, 100)] int limit = 10, CancellationToken ct = default) =>
        Ok(await aiReview.RunBatchAsync(limit, ct));

    /// <summary>Xem bằng chứng máy đã phán cho một bản ghi.</summary>
    /// <remarks>
    /// Trả lượt phán gần nhất: transcript ASR nghe được, văn bản đã chuẩn hoá đem so, hai con số
    /// WER, và lời giải thích. `204` nghĩa là máy chưa từng phán bản này.
    ///
    /// Người duyệt xem được endpoint này, nên **nó phá tính duyệt mù giữa máy và người** nếu
    /// giao diện hiện nó ra trước khi người bỏ phiếu. Cân nhắc khi gắn vào màn hình duyệt.
    /// </remarks>
    [HttpGet("api/recordings/{id}/ai-review")]
    [Authorize(Roles = "Reviewer,TaskManager,Admin")]
    [ProducesResponseType(typeof(AiReviewResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult<AiReviewResultDto>> Latest(string id, CancellationToken ct)
    {
        var latest = await aiReview.GetLatestAsync(id, ct);

        return latest is null ? NoContent() : Ok(latest);
    }
}
