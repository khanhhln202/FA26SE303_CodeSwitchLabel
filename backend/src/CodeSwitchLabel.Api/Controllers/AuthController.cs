using CodeSwitchLabel.Api.Infrastructure;
using CodeSwitchLabel.Services.Abstractions;
using CodeSwitchLabel.Services.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CodeSwitchLabel.Api.Controllers;

[ApiController]
[Route("api/auth")]
[Tags(ApiTags.Authentication)]
public class AuthController(IAuthService authService) : ControllerBase
{
    /// <summary>Đăng nhập và lấy access token.</summary>
    /// <remarks>
    /// Tài khoản demo (chỉ có ở môi trường phát triển), mật khẩu xem trong appsettings.Development.json:
    ///
    /// - admin@codeswitchlabel.local — Admin
    /// - manager@codeswitchlabel.local — Task Manager
    /// - reviewer@codeswitchlabel.local — Reviewer
    /// - speaker1@codeswitchlabel.local — Speaker
    /// - speaker2@codeswitchlabel.local — Speaker
    ///
    /// Chép giá trị accessToken rồi bấm nút **Authorize** ở góc trên bên phải để gọi các endpoint khác.
    /// </remarks>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<LoginResponse>> Login(
        [FromBody] LoginRequest request, CancellationToken ct)
        => Ok(await authService.LoginAsync(request, ct));

    /// <summary>Xác minh Google ID token và đổi token đăng nhập ứng dụng.</summary>
    /// <remarks>
    /// Cho phép đăng nhập bằng tài khoản Google mà không cần mật khẩu ứng dụng. Nếu dịch vụ
    /// Google chưa cấu hình thì trả 503. Chép accessToken rồi bấm **Authorize** để gọi các endpoint khác.
    /// </remarks>
    [HttpPost("google")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<LoginResponse>> GoogleLogin(
        [FromBody] GoogleLoginRequest request, CancellationToken ct)
        => Ok(await authService.GoogleLoginAsync(request, ct));

    /// <summary>Tình nguyện viên tự đăng ký làm Speaker — hoạt động ngay, không qua duyệt.</summary>
    /// <remarks>
    /// Tạo tài khoản Speaker ở trạng thái Active kèm hồ sơ người đọc (nếu gửi).
    /// Email lưu chữ thường; trùng email trả 409. Mật khẩu 8-128 ký tự.
    /// </remarks>
    [HttpPost("register-speaker")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<LoginResponse>> RegisterSpeaker(
        [FromBody] RegisterSpeakerRequest request, CancellationToken ct)
        => Ok(await authService.RegisterSpeakerAsync(request, ct));

    /// <summary>Lưu đồng ý dùng giọng nói cho nghiên cứu, kèm bản giấy đã ký (PDF/ảnh) nếu có.</summary>
    /// <remarks>
    /// Gọi sau khi đăng nhập. Gửi multipart: `version` bắt buộc, `document` tùy chọn.
    /// Chỉ nhận PDF, JPEG, PNG tối đa 10 MB. Bản mới ghi đè bản cũ của cùng người.
    /// </remarks>
    [HttpPost("consent")]
    [Authorize]
    [RequestSizeLimit(10 * 1024 * 1024)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AcceptConsent(
        [FromForm] AcceptConsentRequest request, IFormFile? document, CancellationToken ct)
    {
        if (document is { Length: > 0 })
        {
            var allowed = document.ContentType is "application/pdf" or "image/jpeg" or "image/png";
            if (!allowed)
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Điều khoản đã ký phải là PDF, JPEG hoặc PNG."
                });
            }
        }

        await authService.AcceptConsentAsync(
            User.GetUserId(), request.Version,
            document is { Length: > 0 } ? document.OpenReadStream() : null,
            document?.FileName, document?.ContentType, ct);

        return NoContent();
    }

    /// <summary>Thông tin tài khoản đang đăng nhập.</summary>
    /// <remarks>Đọc từ database chứ không từ token, nên vai và trạng thái luôn là mới nhất.</remarks>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(CurrentUserDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<CurrentUserDto>> Me(CancellationToken ct)
        => Ok(await authService.GetCurrentUserAsync(User.GetUserId(), ct));
}
