using CodeSwitchLabel.Api.Infrastructure;
using CodeSwitchLabel.Services.Abstractions;
using CodeSwitchLabel.Services.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CodeSwitchLabel.Api.Controllers;

/// <summary>
/// Admin quản lý tài khoản: tạo, sửa, đổi vai, khoá, cấp lại mật khẩu.
/// Không có chức năng xoá — bản ghi, lượt duyệt và task đều trỏ tới người dùng, xoá là vỡ dữ liệu.
/// </summary>
[ApiController]
[Route("api/users")]
[Tags("10 · Người dùng")]
[Authorize(Roles = "Admin")]
public class UsersController(IUserService userService) : ControllerBase
{
    /// <summary>Danh sách tài khoản, lọc theo vai, trạng thái, họ tên hoặc email.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<UserListItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<UserListItemDto>>> Search(
        [FromQuery] UserSearchRequest request, CancellationToken ct)
        => Ok(await userService.SearchAsync(request, ct));

    /// <summary>Chi tiết một tài khoản, kèm hồ sơ người đọc và các task đang nhận.</summary>
    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(UserDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserDetailDto>> Get(long id, CancellationToken ct)
        => Ok(await userService.GetAsync(id, ct));

    /// <summary>Tạo tài khoản.</summary>
    /// <remarks>
    /// Hệ thống tự sinh **mật khẩu tạm** và trả về trong `temporaryPassword` — **chỉ đúng lần này**,
    /// database chỉ lưu bản băm. Chép lại ngay để chuyển cho người dùng; họ đổi bằng `PUT /api/me/password`.
    ///
    /// Email được lưu chữ thường; `A@x.com` và `a@x.com` được coi là một tài khoản.
    /// Tài khoản Speaker được tạo sẵn hồ sơ người đọc trống để người đó tự điền.
    /// </remarks>
    [HttpPost]
    [ProducesResponseType(typeof(CreateUserResult), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CreateUserResult>> Create(
        [FromBody] CreateUserRequest request, CancellationToken ct)
    {
        var created = await userService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = created.User.UserId }, created);
    }

    /// <summary>Sửa họ tên hoặc số điện thoại. Trường nào không gửi thì giữ nguyên; gửi chuỗi rỗng để xoá số điện thoại.</summary>
    [HttpPatch("{id:long}")]
    [ProducesResponseType(typeof(UserDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserDetailDto>> Update(
        long id, [FromBody] UpdateUserRequest request, CancellationToken ct)
        => Ok(await userService.UpdateAsync(id, request, ct));

    /// <summary>Đổi vai.</summary>
    /// <remarks>
    /// Có hiệu lực ngay: token cũ của người đó bị từ chối ở request kế tiếp, họ phải đăng nhập lại.
    ///
    /// Bị chặn (409) khi:
    /// - người đó **còn đang nhận task chưa xong** (`user_has_active_tasks`) — giao task cho người khác trước;
    /// - đổi vai của **chính mình** (`cannot_change_own_role`);
    /// - đó là **Admin cuối cùng** đang hoạt động (`last_admin`).
    /// </remarks>
    [HttpPut("{id:long}/role")]
    [ProducesResponseType(typeof(UserDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UserDetailDto>> ChangeRole(
        long id, [FromBody] ChangeRoleRequest request, CancellationToken ct)
        => Ok(await userService.ChangeRoleAsync(id, request.Role!.Value, User.GetUserId(), ct));

    /// <summary>Khoá tài khoản.</summary>
    /// <remarks>
    /// Có hiệu lực ngay: mọi request tiếp theo của người đó nhận 401.
    /// Khoá được cả khi người đó đang nhận task — `activeTasks` trong kết quả cho biết task nào cần giao lại.
    /// Khoá lần hai không báo lỗi. Không khoá được chính mình, cũng không khoá được Admin cuối cùng.
    /// </remarks>
    [HttpPost("{id:long}/lock")]
    [ProducesResponseType(typeof(UserDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UserDetailDto>> Lock(long id, CancellationToken ct)
        => Ok(await userService.LockAsync(id, User.GetUserId(), ct));

    /// <summary>Mở khoá tài khoản.</summary>
    [HttpPost("{id:long}/unlock")]
    [ProducesResponseType(typeof(UserDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserDetailDto>> Unlock(long id, CancellationToken ct)
        => Ok(await userService.UnlockAsync(id, ct));

    /// <summary>Cấp lại mật khẩu tạm khi người dùng quên mật khẩu.</summary>
    /// <remarks>Mật khẩu tạm chỉ trả về đúng lần này. Mật khẩu cũ hết tác dụng ngay.</remarks>
    [HttpPost("{id:long}/reset-password")]
    [ProducesResponseType(typeof(ResetPasswordResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ResetPasswordResult>> ResetPassword(long id, CancellationToken ct)
        => Ok(await userService.ResetPasswordAsync(id, ct));
}

/// <summary>Người dùng tự quản lý tài khoản của mình.</summary>
[ApiController]
[Route("api/me")]
[Tags("1 · Xác thực")]
[Authorize]
public class MeController(IUserService userService) : ControllerBase
{
    /// <summary>Đổi mật khẩu của chính mình.</summary>
    /// <remarks>Phải nhập đúng mật khẩu hiện tại. Mật khẩu mới từ 8 ký tự và khác mật khẩu cũ.</remarks>
    [HttpPut("password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request, CancellationToken ct)
    {
        await userService.ChangeOwnPasswordAsync(User.GetUserId(), request, ct);
        return NoContent();
    }

    /// <summary>Hồ sơ người đọc của chính mình: năm sinh, tỉnh, trình độ tiếng Anh, nghề nghiệp, chuyên ngành.</summary>
    [HttpGet("speaker-profile")]
    [Authorize(Roles = "Speaker")]
    [ProducesResponseType(typeof(SpeakerProfileDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<SpeakerProfileDto>> GetSpeakerProfile(CancellationToken ct)
        => Ok(await userService.GetOwnSpeakerProfileAsync(User.GetUserId(), ct));

    /// <summary>Cập nhật hồ sơ người đọc.</summary>
    /// <remarks>
    /// Ghi đè toàn bộ hồ sơ: trường nào gửi `null` hoặc bỏ trống là xoá trắng trường đó.
    /// Trình độ tiếng Anh tính theo thang IELTS, từ 0.0 đến 9.0.
    /// </remarks>
    [HttpPut("speaker-profile")]
    [Authorize(Roles = "Speaker")]
    [ProducesResponseType(typeof(SpeakerProfileDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<SpeakerProfileDto>> UpdateSpeakerProfile(
        [FromBody] UpdateSpeakerProfileRequest request, CancellationToken ct)
        => Ok(await userService.UpdateOwnSpeakerProfileAsync(User.GetUserId(), request, ct));
}
