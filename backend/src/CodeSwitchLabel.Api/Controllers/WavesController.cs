using CodeSwitchLabel.Api.Infrastructure;
using CodeSwitchLabel.Repositories.Enums;
using CodeSwitchLabel.Services.Abstractions;
using CodeSwitchLabel.Services.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace CodeSwitchLabel.Api.Controllers;

/// <summary>
/// Chiến dịch thu thập — Task Manager lập kế hoạch cho một đợt (thường 1-2 tuần / tháng).
/// Task có thể độc lập (wave_id NULL) rồi gắn vào chiến dịch sau; xoá task khỏi
/// chiến dịch là xoá vĩnh viễn task đó.
/// </summary>
/// <remarks>
/// Chỉ tiêu và thời gian của chiến dịch ràng buộc với các task con, phần lớn do trigger của
/// database chặn: tổng chỉ tiêu task không được vượt chỉ tiêu chiến dịch, hạn task phải nằm
/// trong khoảng ngày của chiến dịch, và không được hạ chỉ tiêu xuống dưới phần đã chia.
/// </remarks>
[ApiController]
[Route("api/waves")]
[Tags(ApiTags.Waves)]
[Authorize(Roles = "TaskManager,Admin")]
public class WavesController(IWaveService waveService) : ControllerBase
{
    // TaskManager và Admin đều tạo được đợt; TaskManager tự gán đợt cho mình
    /// <summary>Tạo chiến dịch mới — ở trạng thái Open (chưa có nhiệm vụ nào).
    /// Chỉ tiêu từ 2000 đến 5000 cặp câu.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(WaveDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<WaveDto>> Create(
        [FromBody] CreateWaveRequest request, CancellationToken ct)
    {
        var created = await waveService.CreateAsync(request, User.GetUserId(), User.IsInRole("Admin"), ct);
        return CreatedAtAction(nameof(Get), new { id = created.WaveId }, created);
    }

    /// <summary>Danh sách chiến dịch, lọc theo trạng thái, có phân trang.</summary>
    /// <remarks>
    /// Mỗi dòng kèm tiến độ: tổng chỉ tiêu đã chia cho các task, số task và số task đã xong.
    /// Task Manager chỉ xem được chiến dịch được giao cho mình (read-only).
    ///
    /// `?sort=` nhận `waveId`, `startDate`, `endDate`, `status`, `createdAt`;
    /// `?dir=asc|desc` (mặc định `asc`). Bỏ trống `sort` thì ra đợt mới nhất trước.
    /// </remarks>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<WaveListItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<WaveListItemDto>>> Search(
        [FromQuery] WaveSearchRequest request, CancellationToken ct)
    {
        var userId = User.GetUserId();
        var roleClaim = User.FindFirst(ClaimTypes.Role)?.Value;
        RoleName? userRole = null;
        if (roleClaim != null && Enum.TryParse<RoleName>(roleClaim.Replace("_", ""), true, out var r))
        {
            userRole = r;
        }

        return Ok(await waveService.SearchAsync(request, userId, userRole, ct));
    }

    /// <summary>
    /// Lấy task thu âm duy nhất của chiến dịch; nếu chưa có thì tạo mới với chỉ tiêu bằng cả chiến dịch.
    /// </summary>
    /// <remarks>
    /// Idempotent: gọi lại nhiều lần vẫn trả về đúng một task thu âm. Chỉ tiêu của task bằng
    /// `targetQty` của chiến dịch, hạn là cuối ngày `endDate` theo giờ Việt Nam.
    ///
    /// `autoFill=true` (mặc định) tự lấp các cặp câu đã duyệt nội dung cho tới khi đủ chỉ tiêu
    /// hoặc hết câu phù hợp; `autoFill=false` chỉ tạo task rỗng để Task Manager tự thêm mục.
    /// </remarks>
    [HttpPost("{id:long}/recording-task")]
    [ProducesResponseType(typeof(TaskDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<TaskDetailDto>> GetOrCreateRecordingTask(
        long id, [FromQuery] bool autoFill = true, CancellationToken ct = default)
        => Ok(await waveService.GetOrCreateRecordingTaskAsync(
            id, autoFill, User.GetUserId(), User.IsInRole("Admin"), ct));

    /// <summary>Chi tiết một chiến dịch.</summary>
    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(WaveDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WaveDto>> Get(long id, CancellationToken ct)
        => Ok(await waveService.GetAsync(id, User.GetUserId(), User.IsInRole("Admin"), ct));

    // TaskManager phụ trách đợt và Admin đều sửa được
    /// <summary>Sửa tên, chủ đề, chỉ tiêu hoặc khoảng ngày. Trường nào để trống thì giữ nguyên.</summary>
    /// <remarks>
    /// Không hạ chỉ tiêu xuống dưới phần đã chia cho các task — database sẽ từ chối.
    ///
    /// body **không còn nhận `status`**. Trạng thái đợt là hàm của các nhiệm vụ bên
    /// trong (chưa có nhiệm vụ nào → `Open`, có nhiệm vụ → `InProgress`, mọi nhiệm vụ đều
    /// `Completed` → `Completed`) và được tính lại mỗi khi nhiệm vụ đổi, nên không có
    /// đường nào để bấm tay đặt nó.
    /// </remarks>
    [HttpPatch("{id:long}")]
    [ProducesResponseType(typeof(WaveDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<WaveDto>> Update(
        long id, [FromBody] UpdateWaveRequest request, CancellationToken ct)
        => Ok(await waveService.UpdateAsync(id, request, User.GetUserId(), User.IsInRole("Admin"), ct));

    // Xoá đợt — các task con tự động thành task độc lập nhờ ON DELETE SET NULL
    /// <summary>Xoá chiến dịch. Task đang gắn tự động thành task độc lập (wave_id = null).</summary>
    [HttpDelete("{id:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        await waveService.DeleteAsync(id, User.GetUserId(), User.IsInRole("Admin"), ct);
        return NoContent();
    }

    /// <summary>Xoá vĩnh viễn một task khỏi chiến dịch.</summary>
    /// <remarks>
    /// Task biến mất khỏi đợt, trang Speaker/Reviewer và API chi tiết task. Lượt giao, mục công việc,
    /// hàng đợi và liên kết task_recording xoá theo task; bản ghi thu âm và lượt duyệt được giữ lại
    /// với task_id NULL. Xoá được cả task đã hoàn thành hoặc đã có dữ liệu nộp.
    /// </remarks>
    [HttpDelete("{id:long}/tasks/{taskId:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteTask(long id, long taskId, CancellationToken ct)
    {
        await waveService.DeleteTaskAsync(id, taskId, User.GetUserId(), User.IsInRole("Admin"), ct);
        return NoContent();
    }

    /// <summary>Giao hoặc lấy lại chiến dịch cho Task Manager. Chỉ Admin.</summary>
    /// <remarks>
    /// Truyền assignedToUserId = null để lấy lại (unassign). User phải có role task_manager.
    /// Database trigger sẽ chặn nếu gán cho role khác.
    /// </remarks>
    [HttpPost("{id:long}/assign")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(WaveDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<WaveDto>> Assign(
        long id, [FromBody] AssignWaveRequest request, CancellationToken ct)
        => Ok(await waveService.AssignAsync(id, request.AssignedToUserId, ct));
}

public record AssignWaveRequest
{
    public long? AssignedToUserId { get; init; }
}