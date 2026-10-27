using CodeSwitchLabel.Api.Infrastructure;
using CodeSwitchLabel.Services.Abstractions;
using CodeSwitchLabel.Services.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CodeSwitchLabel.Api.Controllers;

/// <summary>
/// TEAM_015 — hàng chờ đề xuất câu (yêu cầu 2.5): câu báo lỗi / đề xuất sửa do Speaker
/// gửi trên các câu đã duyệt nội dung, chờ Reviewer chốt.
///
/// Hàng chờ này KHÔNG phải `script_queue`: đề xuất là việc riêng chờ quyết định, không
/// phải một chặng mà mọi câu đều đi qua. Vì vậy nó có endpoint và cơ chế giữ chỗ riêng —
/// Reviewer đọc danh sách, mở một đề xuất rồi quyết định, không cần claim theo TTL.
/// </summary>
[ApiController]
[Tags(ApiTags.Reviews)]
[Authorize]
public class ProposalsController(IScriptProposalService proposalService) : ControllerBase
{
    /// <summary>Hàng chờ đề xuất — mặc định cũ nhất trước, vì đây là hàng chờ.</summary>
    /// <remarks>
    /// Mỗi mục kèm sẵn cả hai phiên bản để đối chiếu: `original` là câu đang có trong kho,
    /// `proposed` là bản sửa Speaker đề xuất (chỉ có với đề xuất `edit`). Đề xuất `report`
    /// có `proposed` null — Reviewer tự đưa ra bản chốt khi duyệt. `draft` là bản nháp
    /// Reviewer đã lưu trên máy chủ (nếu có).
    ///
    /// Lọc theo `kind`, `domain`, tìm theo `keyword` (nội dung câu, bản sửa, tên người gửi).
    /// `?sort=` nhận `proposalId`, `createdAt`, `kind`, `scriptId`.
    /// </remarks>
    [HttpGet("api/reviewer/proposals")]
    [Authorize(Roles = "Reviewer")]
    [ProducesResponseType(typeof(PagedResult<ProposalQueueItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<ProposalQueueItemDto>>> Queue(
        [FromQuery] ProposalQueueQuery query, CancellationToken ct)
        => Ok(await proposalService.ListAsync(query, ct));

    /// <summary>Lưu bản nháp Reviewer đã kiểm luật lên máy chủ.</summary>
    /// <remarks>
    /// Bản nháp phải đúng luật của cặp câu (giữ số từ tiếng Anh, quan hệ Anh–Việt) và chỉ
    /// lưu được khi đề xuất còn chờ. Quyết định duyệt đọc bản nháp này thay vì tin nội dung
    /// client gửi kèm ở bước chốt.
    /// </remarks>
    [HttpPut("api/reviewer/proposals/{id:long}/draft")]
    [Authorize(Roles = "Reviewer")]
    [ProducesResponseType(typeof(ProposalDraftDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ProposalDraftDto>> SaveDraft(
        long id, [FromBody] SaveProposalDraftRequest request, CancellationToken ct)
        => Ok(await proposalService.SaveDraftAsync(id, User.GetUserId(), request, ct));

    /// <summary>Chốt một đề xuất: duyệt (ghi nội dung mới vào câu), từ chối (giữ nguyên câu), hoặc xoá câu.</summary>
    /// <remarks>
    /// **Duyệt:**
    /// - Bản chốt lấy từ bản nháp đã lưu; nếu chưa lưu thì nhận `appliedCsContent` +
    ///   `appliedViContent`, cuối cùng mới lấy bản sửa của đề xuất `edit`. Đề xuất
    ///   `report` không có bản sẵn nên phải lưu nháp hoặc gửi kèm.
    /// - Người duyệt phải được phân đúng chủ đề của câu (như duyệt nội dung).
    /// - Nội dung mới được ghi vào câu kèm **một lượt duyệt `edited`** trong lịch sử câu;
    ///   câu vẫn ở trạng thái `validated`.
    ///
    /// Chốt bằng bản nháp đã lưu (`PUT .../draft`) là đường chính; `applied*` vẫn được nhận
    /// để client cũ không gãy, và được lưu lại thành bản nháp cuối.
    ///
    /// **Từ chối:** bắt buộc ghi `comment` làm lý do; câu giữ nguyên nội dung.
    ///
    /// **Xoá:** câu chuyển `deactivated` (soft-delete) và rời mọi hàng đợi; đề xuất và
    /// lịch sử vẫn còn. Dùng khi câu không thể sửa để dùng được.
    ///
    /// Cả ba đường đều trả câu về hàng đợi ghi âm khi câu còn dùng được và chưa có bản ghi.
    /// Quyết định rồi thì không đổi được nữa — gọi lại trả **409**.
    /// </remarks>
    [HttpPost("api/reviewer/proposals/{id:long}/resolve")]
    [Authorize(Roles = "Reviewer")]
    [ProducesResponseType(typeof(ProposalResolutionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ProposalResolutionDto>> Resolve(
        long id, [FromBody] ResolveProposalRequest request, CancellationToken ct)
        => Ok(await proposalService.ResolveAsync(id, User.GetUserId(), request, ct));
}
