using CodeSwitchLabel.Api.Infrastructure;
using CodeSwitchLabel.Services.Abstractions;
using CodeSwitchLabel.Services.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CodeSwitchLabel.Api.Controllers;

[ApiController]
[Route("api/auth")]
[Tags("1 · Xác thực")]
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
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<LoginResponse>> Login(
        [FromBody] LoginRequest request, CancellationToken ct)
        => Ok(await authService.LoginAsync(request, ct));

    /// <summary>Thông tin tài khoản đang đăng nhập.</summary>
    /// <remarks>Đọc từ database chứ không từ token, nên vai và trạng thái luôn là mới nhất.</remarks>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(CurrentUserDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<CurrentUserDto>> Me(CancellationToken ct)
        => Ok(await authService.GetCurrentUserAsync(User.GetUserId(), ct));
}
