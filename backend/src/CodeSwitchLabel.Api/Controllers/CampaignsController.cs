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
/// Task có thể độc lập (campaign_id NULL) rồi gắn vào chiến dịch sau; gỡ task khỏi
/// chiến dịch chỉ detach (giữ lại task), không xoá task.
/// </summary>
/// <remarks>
/// Chỉ tiêu và thời gian của chiến dịch ràng buộc với các task con, phần lớn do trigger của
/// database chặn: tổng chỉ tiêu task không được vượt chỉ tiêu chiến dịch, hạn task phải nằm
/// trong khoảng ngày của chiến dịch, và không được hạ chỉ tiêu xuống dưới phần đã chia.
/// </remarks>
[ApiController]
[Route("api/campaigns")]
[Tags(ApiTags.Campaigns)]
[Authorize(Roles = "TaskManager,Admin")]
public class CampaignsController(ICampaignService campaignService) : ControllerBase
{
    // TEAM_003: TaskManager và Admin đều tạo được đợt; TaskManager tự gán đợt cho mình
    /// <summary>Tạo chiến dịch mới — ở trạng thái Open (chưa có nhiệm vụ nào).
    /// Chỉ tiêu từ 2000 đến 5000 cặp câu.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(CampaignDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<CampaignDto>> Create(
        [FromBody] CreateCampaignRequest request, CancellationToken ct)
    {
        var created = await campaignService.CreateAsync(request, User.GetUserId(), User.IsInRole("Admin"), ct);
        return CreatedAtAction(nameof(Get), new { id = created.CampaignId }, created);
    }

    /// <summary>Danh sách chiến dịch, lọc theo trạng thái, có phân trang.</summary>
    /// <remarks>
    /// Mỗi dòng kèm tiến độ: tổng chỉ tiêu đã chia cho các task, số task và số task đã xong.
    /// Task Manager chỉ xem được chiến dịch được giao cho mình (read-only).
    ///
    /// TEAM_012: `?sort=` nhận `campaignId`, `startDate`, `endDate`, `status`, `createdAt`;
    /// `?dir=asc|desc` (mặc định `asc`). Bỏ trống `sort` thì ra đợt mới nhất trước.
    /// </remarks>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<CampaignListItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<CampaignListItemDto>>> Search(
        [FromQuery] CampaignSearchRequest request, CancellationToken ct)
    {
        var userId = User.GetUserId();
        var roleClaim = User.FindFirst(ClaimTypes.Role)?.Value;
        RoleName? userRole = null;
        if (roleClaim != null && Enum.TryParse<RoleName>(roleClaim.Replace("_", ""), true, out var r))
        {
            userRole = r;
        }

        return Ok(await campaignService.SearchAsync(request, userId, userRole, ct));
    }

    /// <summary>Chi tiết một chiến dịch.</summary>
    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(CampaignDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CampaignDto>> Get(long id, CancellationToken ct)
        => Ok(await campaignService.GetAsync(id, ct));

    // TEAM_003: TaskManager phụ trách đợt và Admin đều sửa được
    /// <summary>Sửa tên, chủ đề, chỉ tiêu hoặc khoảng ngày. Trường nào để trống thì giữ nguyên.</summary>
    /// <remarks>
    /// Không hạ chỉ tiêu xuống dưới phần đã chia cho các task — database sẽ từ chối.
    ///
    /// TEAM_013: body **không còn nhận `status`**. Trạng thái đợt là hàm của các nhiệm vụ bên
    /// trong (chưa có nhiệm vụ nào → `Open`, có nhiệm vụ → `InProgress`, mọi nhiệm vụ đều
    /// `Completed` → `Completed`) và được tính lại mỗi khi nhiệm vụ đổi, nên không có
    /// đường nào để bấm tay đặt nó.
    /// </remarks>
    [HttpPatch("{id:long}")]
    [ProducesResponseType(typeof(CampaignDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<CampaignDto>> Update(
        long id, [FromBody] UpdateCampaignRequest request, CancellationToken ct)
        => Ok(await campaignService.UpdateAsync(id, request, User.GetUserId(), User.IsInRole("Admin"), ct));

    // TEAM_003: Xoá đợt — các task con tự động thành task độc lập nhờ ON DELETE SET NULL
    /// <summary>Xoá chiến dịch. Task đang gắn tự động thành task độc lập (campaign_id = null).</summary>
    [HttpDelete("{id:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        await campaignService.DeleteAsync(id, User.GetUserId(), User.IsInRole("Admin"), ct);
        return NoContent();
    }

    /// <summary>Gỡ task khỏi chiến dịch (detach): task còn tồn tại với campaign_id NULL, giữ assignments/items.</summary>
    /// <remarks>
    /// Không xoá task, không xoá task_script/task_recording/assignment.
    /// </remarks>
    [HttpDelete("{id:long}/tasks/{taskId:long}")]
    [ProducesResponseType(typeof(TaskDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TaskDetailDto>> DetachTask(
        long id, long taskId, CancellationToken ct)
        => Ok(await campaignService.DetachTaskAsync(id, taskId, User.GetUserId(), User.IsInRole("Admin"), ct));

    /// <summary>Giao hoặc lấy lại chiến dịch cho Task Manager. Chỉ Admin.</summary>
    /// <remarks>
    /// Truyền assignedToUserId = null để lấy lại (unassign). User phải có role task_manager.
    /// Database trigger sẽ chặn nếu gán cho role khác.
    /// </remarks>
    [HttpPost("{id:long}/assign")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(CampaignDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<CampaignDto>> Assign(
        long id, [FromBody] AssignCampaignRequest request, CancellationToken ct)
        => Ok(await campaignService.AssignAsync(id, request.AssignedToUserId, ct));
}

public record AssignCampaignRequest
{
    public long? AssignedToUserId { get; init; }
}