using CodeSwitchLabel.Api.Infrastructure;
using CodeSwitchLabel.Services.Abstractions;
using CodeSwitchLabel.Services.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CodeSwitchLabel.Api.Controllers;

/// <summary>Nội dung bản cam kết — thứ người dùng đọc trước khi bấm đồng ý.</summary>
/// <remarks>
/// Ghi nhận ai đã đồng ý thì nằm ở `POST /api/me/consent` và `GET /api/me/consent`.
/// Đây là phần NỘI DUNG: chữ của từng phiên bản.
/// </remarks>
[ApiController]
[Tags(ApiTags.Account)]
public class ConsentController(IConsentService consent) : ControllerBase
{
    /// <summary>Bản cam kết đang hiệu lực.</summary>
    /// <remarks>
    /// **Không cần đăng nhập** — người ta phải đọc được điều khoản TRƯỚC khi tạo tài khoản
    /// hay bấm đồng ý. Bắt đăng nhập ở đây là bắt người dùng đồng ý với thứ chưa được đọc.
    ///
    /// `body` giữ nguyên xuống dòng; hiện bằng khối `white-space: pre-wrap`.
    ///
    /// Trả **404 `consent_document_missing`** khi chưa có bản nào hiệu lực — lúc đó đừng cho
    /// bấm đồng ý, vì `POST /api/me/consent` cũng sẽ từ chối.
    /// </remarks>
    [HttpGet("api/consent")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ConsentDocumentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ConsentDocumentDto>> Active(CancellationToken ct)
        => Ok(await consent.GetActiveDocumentAsync(ct));

    /// <summary>Nội dung một phiên bản cụ thể, kể cả bản đã cũ.</summary>
    /// <remarks>
    /// Dùng để người đã ký đọc lại **đúng chữ mình từng đồng ý**: `GET /api/me/consent` trả
    /// về `version`, đưa mã đó vào đây là ra nội dung.
    ///
    /// Phiên bản có trước khi hệ thống lưu nội dung (migration V39) vẫn trả về được, nhưng
    /// `body` của chúng nói thẳng là nội dung không khôi phục được.
    /// </remarks>
    [HttpGet("api/consent/{version}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ConsentDocumentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ConsentDocumentDto>> ByVersion(string version, CancellationToken ct)
    {
        var doc = await consent.GetDocumentAsync(version, ct);

        return doc is null
            ? NotFound(new ProblemDetails { Title = $"Không có bản cam kết phiên bản \"{version}\"." })
            : Ok(doc);
    }

    /// <summary>Mọi phiên bản đã phát hành, mới nhất trước.</summary>
    [HttpGet("api/admin/consent-documents")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(IReadOnlyList<ConsentDocumentDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ConsentDocumentDto>>> List(CancellationToken ct)
        => Ok(await consent.ListDocumentsAsync(ct));

    /// <summary>Phát hành một phiên bản cam kết mới.</summary>
    /// <remarks>
    /// Bản đang hiệu lực tự lui thành bản cũ, và **vẫn đọc lại được** — đó là lý do bảng này
    /// tồn tại: tra được người ký hồi tháng trước đã đồng ý chữ gì.
    ///
    /// **Không có endpoint sửa bản đã phát hành.** Phiên bản đã có người đồng ý là bằng
    /// chứng; đổi chữ trong đó là làm sai lệch thứ người ta đã ký. Sửa chữ = phát hành mã mới.
    /// Trùng mã trả **409 `consent_version_exists`**.
    /// </remarks>
    [HttpPost("api/admin/consent-documents")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ConsentDocumentDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ConsentDocumentDto>> Publish(
        [FromBody] PublishConsentDocumentRequest request, CancellationToken ct)
    {
        var created = await consent.PublishDocumentAsync(request, User.GetUserId(), ct);

        return CreatedAtAction(nameof(ByVersion), new { version = created.Version }, created);
    }
}
