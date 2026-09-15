using CodeSwitchLabel.Api.Infrastructure;
using CodeSwitchLabel.Services.Abstractions;
using CodeSwitchLabel.Services.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CodeSwitchLabel.Api.Controllers;

/// <summary>Luồng làm việc của Speaker.</summary>
[ApiController]
[Route("api/speaker")]
[Tags("3 · Speaker")]
[Authorize(Roles = "Speaker")]
public class SpeakerController(
    IScriptAssignmentService assignmentService,
    IScriptService scriptService) : ControllerBase
{
    /// <summary>Lấy script tiếp theo để thu âm.</summary>
    /// <remarks>
    /// Hệ thống chọn một script đã được duyệt mà bạn chưa bỏ qua và chưa thu.
    /// Bản thu từng bị từ chối thì script đó vẫn quay lại để bạn thu lần nữa.
    ///
    /// Truyền taskId để chỉ lấy script nằm trong phạm vi một task cụ thể —
    /// đúng mô hình giao việc của ERD, nơi Task Manager chốt sẵn danh sách
    /// script cho từng task qua bảng task_script.
    ///
    /// **204 No Content** nghĩa là hết script phù hợp. Đó là trạng thái bình thường,
    /// không phải lỗi — giao diện nên hiện "Bạn đã đọc hết phần được giao".
    /// </remarks>
    [HttpGet("scripts/next")]
    [ProducesResponseType(typeof(NextScriptDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult<NextScriptDto>> Next(
        [FromQuery] long? taskId, CancellationToken ct)
    {
        var next = await assignmentService.GetNextAsync(User.GetUserId(), taskId, ct);
        return next is null ? NoContent() : Ok(next);
    }

    /// <summary>Bỏ qua một script.</summary>
    /// <remarks>
    /// Hệ thống ghi lại và **không phát lại script đó cho bạn** nữa.
    /// Người khác vẫn đọc được bình thường — bỏ qua là lựa chọn cá nhân,
    /// khác với từ chối nội dung ở <c>POST /api/scripts/{id}/review</c>,
    /// nơi bạn nói rằng script này có vấn đề với mọi người.
    /// </remarks>
    [HttpPost("scripts/{id:long}/skip")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Skip(
        long id, [FromQuery] long? taskId, CancellationToken ct)
    {
        await assignmentService.SkipAsync(id, User.GetUserId(), taskId, ct);
        return NoContent();
    }

    /// <summary>Đóng góp một script mới.</summary>
    /// <remarks>Vào trạng thái chờ duyệt, phải được duyệt nội dung mới vào lưu thông.</remarks>
    [HttpPost("scripts/contribute")]
    [ProducesResponseType(typeof(ScriptDetailDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ScriptDetailDto>> Contribute(
        [FromBody] CreateScriptRequest request, CancellationToken ct)
    {
        var created = await scriptService.ContributeAsync(request, User.GetUserId(), ct);
        return CreatedAtAction(nameof(ScriptsController.Get), "Scripts",
            new { id = created.ScriptId }, created);
    }
}
