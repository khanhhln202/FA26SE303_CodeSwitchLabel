using System.ComponentModel.DataAnnotations;
using CodeSwitchLabel.Api.Infrastructure;
using CodeSwitchLabel.Services.Abstractions;
using CodeSwitchLabel.Services.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CodeSwitchLabel.Api.Controllers;

/// <summary>Danh mục lý do — Reviewer và Speaker đọc để chọn khi từ chối.</summary>
[ApiController]
[Tags("4 · Danh mục")]
[Authorize]
public class ReasonsController(IReasonService reasonService) : ControllerBase
{
    /// <summary>Lý do từ chối BẢN GHI ÂM, chia theo ba nhóm lỗi của đề tài.</summary>
    /// <remarks>
    /// Mặc định chỉ trả mục đang bật. Admin xem cả mục đã ẩn bằng activeOnly=false —
    /// lý do không bao giờ bị xoá cứng, chỉ ẩn đi, nếu không thống kê
    /// "nguyên nhân từ chối phổ biến" sẽ vỡ khi ai đó xoá một mục đã dùng nhiều lần.
    /// </remarks>
    [HttpGet("api/rejection-reasons")]
    [ProducesResponseType(typeof(IEnumerable<ReasonDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<ReasonDto>>> RejectionReasons(
        [FromQuery] bool activeOnly = true, CancellationToken ct = default)
        => Ok(await reasonService.GetRejectionReasonsAsync(activeOnly, ct));

    /// <summary>Lý do từ chối NỘI DUNG SCRIPT, dùng khi duyệt văn bản trước lúc thu.</summary>
    [HttpGet("api/script-error-reasons")]
    [ProducesResponseType(typeof(IEnumerable<ReasonDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<ReasonDto>>> ScriptErrorReasons(
        [FromQuery] bool activeOnly = true, CancellationToken ct = default)
        => Ok(await reasonService.GetScriptErrorReasonsAsync(activeOnly, ct));
}

/// <summary>Tham số hệ thống — đổi được lúc chạy, không phải deploy lại.</summary>
[ApiController]
[Route("api/admin/config")]
[Tags("5 · Cấu hình hệ thống")]
[Authorize(Roles = "Admin")]
public class SystemConfigController(ISystemConfigService configService) : ControllerBase
{
    /// <summary>Toàn bộ tham số hệ thống.</summary>
    /// <remarks>
    /// Mọi ngưỡng chất lượng và quy tắc số lượng nằm ở đây thay vì nằm trong code.
    ///
    /// Các giá trị hiện tại là ĐỀ XUẤT của nhóm, chưa được giảng viên duyệt —
    /// mô tả đề tài không đưa ra con số nào.
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

public record UpdateConfigRequest
{
    [Required(ErrorMessage = "Giá trị không được để trống.")]
    [StringLength(1000)]
    public string Value { get; init; } = string.Empty;
}
