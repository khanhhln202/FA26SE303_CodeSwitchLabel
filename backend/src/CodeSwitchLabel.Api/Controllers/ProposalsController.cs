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
    /// có `proposed` null — Reviewer tự đưa ra bản chốt khi duyệt.
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

    /// <summary>Chốt một đề xuất: duyệt (ghi nội dung mới vào câu) hoặc từ chối (giữ nguyên câu).</summary>
    /// <remarks>
    /// **Duyệt:**
    /// - Bắt buộc có bản chốt: gửi `appliedCsContent` + `appliedViContent` (Reviewer được
    ///   chỉnh thêm so với bản đề xuất), không gửi thì lấy bản sửa của đề xuất `edit`.
    ///   Đề xuất `report` không có bản sẵn nên **bắt buộc** gửi — duyệt một báo lỗi mà
    ///   không sửa gì thì câu vẫn sai như cũ.
    /// - Người duyệt phải được phân đúng chủ đề của câu (như duyệt nội dung).
    /// - Nội dung mới được ghi vào câu kèm **một lượt duyệt `edited`** trong lịch sử câu;
    ///   câu vẫn ở trạng thái `validated`.
    ///
    /// **Từ chối:** bắt buộc ghi `comment` làm lý do; câu giữ nguyên nội dung.
    ///
    /// Cả hai đường đều trả câu về hàng đợi ghi âm (nếu câu vẫn dùng được và chưa có
    /// bản ghi âm nào). Quyết định rồi thì không đổi được nữa — gọi lại trả **409**.
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
