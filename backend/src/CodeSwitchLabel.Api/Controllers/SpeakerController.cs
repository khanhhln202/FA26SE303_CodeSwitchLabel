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
    IScriptService scriptService,
    ITaskService taskService) : ControllerBase
{
    /// <summary>Lấy cặp câu tiếp theo để thu âm.</summary>
    /// <remarks>
    /// Mỗi cặp câu cần **hai bản ghi**: một bản đọc câu chen tiếng Anh (cs) và một bản đọc câu
    /// thuần Việt (vi). `remainingVariants` cho biết bạn còn nợ bản nào.
    ///
    /// Một cặp câu chỉ do **một người đọc** thu, để hai bản là cùng một giọng. Vì vậy hệ thống
    /// không phát lại câu mà người khác đã thu.
    ///
    /// Truyền `taskId` để chỉ lấy câu trong phạm vi một task. Task đó phải là task thu âm
    /// **đang giao cho chính bạn**, nếu không trả **403**.
    ///
    /// **204 No Content** nghĩa là hết câu phù hợp.
    /// </remarks>
    [HttpGet("scripts/next")]
    [ProducesResponseType(typeof(NextScriptDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<NextScriptDto>> Next([FromQuery] long? taskId, CancellationToken ct)
    {
        var next = await assignmentService.GetNextAsync(User.GetUserId(), taskId, ct);
        return next is null ? NoContent() : Ok(next);
    }

    /// <summary>Đóng góp một cặp câu mới.</summary>
    /// <remarks>
    /// Phải gửi đủ cả câu chen tiếng Anh lẫn câu thuần Việt tương đương, kèm nhãn [vi]/[en].
    /// Câu đóng góp nằm ở trạng thái chờ duyệt nội dung.
    ///
    /// Thấy câu được giao không tự nhiên hay sai chính tả thì dùng
    /// `POST /api/scripts/{id}/review` để sửa hoặc từ chối, thay vì bỏ qua.
    /// </remarks>
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

    /// <summary>Tiến độ thu âm của chính mình.</summary>
    /// <remarks>
    /// Tổng số bản đã nộp, số bản được duyệt đạt, và từng task thu âm đang giao.
    /// Chỉ tiêu task thu âm đếm theo **cặp câu**: một cặp chỉ tính là xong khi **cả hai** bản
    /// cs và vi đều được duyệt đạt.
    /// </remarks>
    [HttpGet("progress")]
    [ProducesResponseType(typeof(SpeakerProgressDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<SpeakerProgressDto>> Progress(CancellationToken ct)
        => Ok(await taskService.GetSpeakerProgressAsync(User.GetUserId(), ct));
}
