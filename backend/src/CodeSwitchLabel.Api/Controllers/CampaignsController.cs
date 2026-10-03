using CodeSwitchLabel.Api.Infrastructure;
using CodeSwitchLabel.Services.Abstractions;
using CodeSwitchLabel.Services.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CodeSwitchLabel.Api.Controllers;

/// <summary>
/// Chiến dịch thu thập — Task Manager lập kế hoạch cho một đợt (thường 1-2 tuần / tháng).
/// Mọi task đều thuộc một chiến dịch, nên phải có chiến dịch trước khi tạo task.
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
    /// <summary>Tạo chiến dịch mới — ở trạng thái Draft. Chỉ tiêu từ 2000 đến 5000 cặp câu.</summary>
    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(CampaignDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<CampaignDto>> Create(
        [FromBody] CreateCampaignRequest request, CancellationToken ct)
    {
        var created = await campaignService.CreateAsync(request, User.GetUserId(), ct);
        return CreatedAtAction(nameof(Get), new { id = created.CampaignId }, created);
    }

    /// <summary>Danh sách chiến dịch, lọc theo trạng thái, có phân trang.</summary>
    /// <remarks>
    /// Mỗi dòng kèm tiến độ: tổng chỉ tiêu đã chia cho các task, số task và số task đã xong.
    /// </remarks>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<CampaignListItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<CampaignListItemDto>>> Search(
        [FromQuery] CampaignSearchRequest request, CancellationToken ct)
        => Ok(await campaignService.SearchAsync(request, ct));

    /// <summary>Chi tiết một chiến dịch.</summary>
    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(CampaignDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CampaignDto>> Get(long id, CancellationToken ct)
        => Ok(await campaignService.GetAsync(id, ct));

    /// <summary>Sửa tên, chỉ tiêu, khoảng ngày hoặc trạng thái. Trường nào để trống thì giữ nguyên.</summary>
    /// <remarks>
    /// Không hạ chỉ tiêu xuống dưới phần đã chia cho các task — database sẽ từ chối.
    /// </remarks>
    [HttpPatch("{id:long}")]
    [ProducesResponseType(typeof(CampaignDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<CampaignDto>> Update(
        long id, [FromBody] UpdateCampaignRequest request, CancellationToken ct)
        => Ok(await campaignService.UpdateAsync(id, request, ct));
}