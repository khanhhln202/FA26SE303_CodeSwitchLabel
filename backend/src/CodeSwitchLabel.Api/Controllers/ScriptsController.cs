using CodeSwitchLabel.Api.Infrastructure;
using CodeSwitchLabel.Services.Abstractions;
using CodeSwitchLabel.Services.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CodeSwitchLabel.Api.Controllers;

/// <summary>
/// Kho script — đoạn văn bản để Speaker đọc.
/// Trước đây ERD gọi là "sentence", đổi tên theo yêu cầu của giảng viên.
/// </summary>
[ApiController]
[Route("api/scripts")]
[Tags("2 · Kho script")]
[Authorize]
public class ScriptsController(IScriptService scriptService) : ControllerBase
{
    /// <summary>Tìm kiếm script, có lọc theo trạng thái và chủ đề, có phân trang.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<ScriptListItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<ScriptListItemDto>>> Search(
        [FromQuery] ScriptSearchRequest request, CancellationToken ct)
        => Ok(await scriptService.SearchAsync(request, ct));

    /// <summary>Chi tiết một script, kèm toàn bộ lịch sử duyệt nội dung.</summary>
    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(ScriptDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ScriptDetailDto>> Get(long id, CancellationToken ct)
        => Ok(await scriptService.GetAsync(id, ct));

    /// <summary>Admin thêm script mới — vào thẳng trạng thái đã duyệt.</summary>
    /// <remarks>
    /// Bỏ trống enWordCount thì hệ thống tự ước lượng số từ tiếng Anh.
    ///
    /// Ước lượng chỉ là phỏng đoán: nó dựa vào việc từ có mang dấu tiếng Việt hay không,
    /// nên nhận nhầm những từ tiếng Việt viết không dấu. Người nhập biết rõ hơn thì
    /// gửi kèm con số của mình để ghi đè.
    /// </remarks>
    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ScriptDetailDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ScriptDetailDto>> Create(
        [FromBody] CreateScriptRequest request, CancellationToken ct)
    {
        var created = await scriptService.CreateAsync(request, User.GetUserId(), ct);
        return CreatedAtAction(nameof(Get), new { id = created.ScriptId }, created);
    }

    /// <summary>Duyệt nội dung một script: chấp nhận, sửa, hoặc từ chối.</summary>
    /// <remarks>
    /// Đây là use case "Review Text" của đề tài. Mỗi lượt duyệt được ghi lại thành
    /// một dòng riêng trong lịch sử, nên duyệt lại nhiều lần vẫn truy được ai làm gì khi nào.
    ///
    /// Chọn **Edited** thì bắt buộc gửi kèm editedContent.
    /// Chọn **Rejected** thì bắt buộc gửi kèm errorReasonCode lấy từ danh mục
    /// tại <c>GET /api/script-error-reasons</c>.
    ///
    /// Lưu ý: script đã có bản ghi âm thì KHÔNG sửa nội dung được nữa — sửa sẽ làm
    /// transcript của các bản ghi cũ không còn khớp với âm thanh.
    /// </remarks>
    [HttpPost("{id:long}/review")]
    [Authorize(Roles = "Admin,Speaker,Reviewer")]
    [ProducesResponseType(typeof(ScriptDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ScriptDetailDto>> Review(
        long id, [FromBody] ReviewScriptRequest request, CancellationToken ct)
        => Ok(await scriptService.ReviewAsync(id, User.GetUserId(), request, ct));
}
