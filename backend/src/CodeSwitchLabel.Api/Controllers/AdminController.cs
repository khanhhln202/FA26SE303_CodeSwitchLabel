using System.ComponentModel.DataAnnotations;
using CodeSwitchLabel.Api.Infrastructure;
using CodeSwitchLabel.Services.Abstractions;
using CodeSwitchLabel.Services.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CodeSwitchLabel.Api.Controllers;

/// <summary>Danh mục lý do — Reviewer và Speaker đọc để chọn khi từ chối.</summary>
[ApiController]
[Tags(ApiTags.RejectionReasons)]
[Authorize]
public class ReasonsController(IReasonService reasonService) : ControllerBase
{
    /// <summary>Lý do từ chối BẢN GHI ÂM, chia theo bốn nhóm: nội dung, âm thanh, phát âm, khác.</summary>
    /// <remarks>
    /// Lý do thuộc nhóm **content** có tác dụng đặc biệt: database tự đưa cặp câu về trạng thái
    /// chờ duyệt nội dung, vì lỗi nằm ở văn bản chứ không phải ở giọng đọc.
    /// </remarks>
    [HttpGet("api/rejection-reasons")]
    [ProducesResponseType(typeof(IEnumerable<ReasonDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<ReasonDto>>> RejectionReasons(
        [FromQuery] bool activeOnly = true, CancellationToken ct = default)
        => Ok(await reasonService.GetRejectionReasonsAsync(activeOnly, ct));

    /// <summary>Lý do từ chối NỘI DUNG CÂU, dùng khi duyệt văn bản trước lúc thu.</summary>
    [HttpGet("api/script-error-reasons")]
    [ProducesResponseType(typeof(IEnumerable<ReasonDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<ReasonDto>>> ScriptErrorReasons(
        [FromQuery] bool activeOnly = true, CancellationToken ct = default)
        => Ok(await reasonService.GetScriptErrorReasonsAsync(activeOnly, ct));
}

/// <summary>Tham số hệ thống — đổi được lúc chạy, không phải deploy lại.</summary>
[ApiController]
[Route("api/admin/config")]
[Tags(ApiTags.SystemConfiguration)]
[Authorize(Roles = "Admin")]
public class SystemConfigController(ISystemConfigService configService) : ControllerBase
{
    /// <summary>Toàn bộ tham số hệ thống.</summary>
    /// <remarks>
    /// Bảy tham số đầu do lược đồ của nhóm tạo sẵn. Hai tham số thời lượng bản ghi là đề xuất
    /// của nhóm backend, **chưa được giảng viên duyệt**.
    ///
    /// Lưu ý: `review.rounds_required` phải giữ bằng 3, vì trigger chốt theo đa số trong database
    /// đang ghim cứng con số này.
    /// </remarks>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<SystemConfigDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<SystemConfigDto>>> GetAll(CancellationToken ct)
        => Ok(await configService.GetAllAsync(ct));

    /// <summary>Đổi giá trị một tham số.</summary>
    [HttpPut("{key}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        string key, [FromBody] UpdateConfigRequest request, CancellationToken ct)
    {
        await configService.UpdateAsync(key, request.Value, User.GetUserId(), ct);
        return NoContent();
    }
}

/// <summary>
/// Số liệu tổng hợp cho Admin. Mọi con số đọc thẳng từ view có sẵn trong lược đồ,
/// nên số trên màn hình và số trong báo cáo SQL luôn khớp nhau.
/// </summary>
[ApiController]
[Route("api/admin")]
[Tags(ApiTags.Statistics)]
[Authorize(Roles = "Admin")]
public class StatisticsController(IStatisticsService statistics) : ControllerBase
{
    /// <summary>Bảng tổng quan: số câu, số bản ghi, tổng thời lượng đã duyệt đạt, số người tham gia.</summary>
    [HttpGet("dashboard")]
    [ProducesResponseType(typeof(DashboardDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<DashboardDto>> Dashboard(CancellationToken ct)
        => Ok(await statistics.GetDashboardAsync(ct));

    /// <summary>Chất lượng theo từng người đọc: số bản nộp, số đạt, số bị từ chối, tỉ lệ đạt.</summary>
    [HttpGet("statistics/speakers")]
    [ProducesResponseType(typeof(IEnumerable<SpeakerQualityDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<SpeakerQualityDto>>> Speakers(CancellationToken ct)
        => Ok(await statistics.GetSpeakerQualityAsync(ct));

    /// <summary>Số lượt duyệt của từng Reviewer, tách theo đạt và từ chối.</summary>
    [HttpGet("statistics/reviewers")]
    [ProducesResponseType(typeof(IEnumerable<ReviewerQualityDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<ReviewerQualityDto>>> Reviewers(CancellationToken ct)
        => Ok(await statistics.GetReviewerQualityAsync(ct));

    /// <summary>Nguyên nhân từ chối phổ biến — xếp theo số lần bị dùng.</summary>
    [HttpGet("statistics/rejection-reasons")]
    [ProducesResponseType(typeof(IEnumerable<RejectionStatDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<RejectionStatDto>>> RejectionStats(CancellationToken ct)
        => Ok(await statistics.GetRejectionStatsAsync(ct));
}

public record UpdateConfigRequest
{
    [Required(ErrorMessage = "Giá trị không được để trống.")]
    [StringLength(1000)]
    public string Value { get; init; } = string.Empty;
}
