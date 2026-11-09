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
    /// <summary>Cho máy phán một bản ghi.</summary>
    /// <remarks>
    /// Máy lấy audio từ kho, gọi ASR để chuyển thành chữ, rồi so với văn bản đáng ra phải đọc
    /// và tính **tỉ lệ sai từ (WER)**. Tính hai lần — giữ dấu tiếng Việt và bỏ dấu — rồi phán
    /// theo con số nhỏ hơn, vì ASR mất dấu là giới hạn của mô hình chứ không phải lỗi người đọc.
    ///
    /// Ba kết quả:
    /// - `rejected` — WER đạt `ai_review.wer_reject_min`. Máy **bỏ phiếu thật** vào bảng review,
    ///   với lý do `content_mismatch`.
    /// - `approved` — WER trong `ai_review.wer_approve_max` **và** `ai_review.allow_approve_vote`
    ///   đang bật. Mặc định cờ này **tắt**.
    /// - `abstained` — không đủ chắc. Ghi bằng chứng nhưng **không** bỏ phiếu, người duyệt quyết.
    ///
    /// Máy chỉ nêu được `content_mismatch`. Ba lý do còn lại — `background_noise`,
    /// `mispronunciation`, `other` — nó không đo được nên không bao giờ tự chọn.
    ///
    /// Phiếu của máy đi qua đúng đường mà người dùng đi, nên vẫn chịu mọi ràng buộc: không duyệt
    /// hai lần, không duyệt bản mình thu, và **trigger đa số của database có thể chốt bản ghi ngay**
    /// khi phiếu này làm một bên quá bán.
    /// </remarks>
    /// <response code="422">Tính năng đang tắt, chưa cấu hình ASR, hoặc ASR gọi không được.</response>
    /// <response code="409">Bản ghi không còn chờ duyệt, hoặc máy đã duyệt bản này rồi.</response>
    [HttpPost("api/recordings/{id}/ai-review")]
    [Authorize(Roles = "TaskManager,Admin")]
    [ProducesResponseType(typeof(AiReviewResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<AiReviewResultDto>> Analyze(string id, CancellationToken ct) =>
        Ok(await aiReview.AnalyzeAsync(id, ct));

    /// <summary>Cho máy phán một lô bản ghi đang chờ duyệt.</summary>
    /// <remarks>
    /// Chỉ lấy những bản còn `pending_review` mà máy **chưa từng phán**, cũ nhất trước.
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
