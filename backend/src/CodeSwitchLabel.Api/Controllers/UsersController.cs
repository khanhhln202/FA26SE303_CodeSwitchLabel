using CodeSwitchLabel.Api.Infrastructure;
using CodeSwitchLabel.Repositories.Enums;
using CodeSwitchLabel.Services.Abstractions;
using CodeSwitchLabel.Services.Common;
using CodeSwitchLabel.Services.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CodeSwitchLabel.Api.Controllers;

/// <summary>
/// Admin và Task Manager quản lý tài khoản: tạo, sửa, đổi vai, khoá, cấp lại mật khẩu.
/// Task Manager chỉ thao tác trên Speaker/Reviewer và không nâng vai lên Admin/TaskManager.
/// Không có chức năng xoá — bản ghi, lượt duyệt và task đều trỏ tới người dùng, xoá là vỡ dữ liệu.
/// </summary>
[ApiController]
[Route("api/users")]
[Tags(ApiTags.Users)]
[Authorize(Roles = "Admin,TaskManager")]
public class UsersController(IUserService userService) : ControllerBase
{
    /// <summary>Danh sách tài khoản, lọc theo vai, trạng thái, họ tên hoặc email.</summary>
    /// <remarks>
    /// `?sort=` nhận `userId`, `fullName`, `email`, `status`, `role`, `createdAt`;
    /// `?dir=asc|desc` (mặc định `asc`). Bỏ trống `sort` thì ra họ tên A-Z.
    /// </remarks>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<UserListItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<UserListItemDto>>> Search(
        [FromQuery] UserSearchRequest request, CancellationToken ct)
        => Ok(await userService.SearchAsync(request, ct));

    /// <summary>Bảng Speaker của Admin: họ tên, email, giới tính, trạng thái và số cặp đã duyệt.</summary>
    /// <remarks>
    /// Một cặp được đếm khi cả bản CS và bản VI đều đã duyệt đạt. Có `waveId` thì chỉ đếm cặp
    /// trong đợt đó và chỉ trả về người có ít nhất một cặp như vậy; bỏ trống thì cộng tất cả.
    /// </remarks>
    [HttpGet("speakers")]
    [ProducesResponseType(typeof(PagedResult<SpeakerListItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<SpeakerListItemDto>>> SearchSpeakers(
        [FromQuery] SpeakerSearchRequest request, CancellationToken ct)
        => Ok(await userService.SearchSpeakersAsync(request, ct));

    /// <summary>Chi tiết một tài khoản, kèm hồ sơ người đọc và các task đang nhận.</summary>
    /// <remarks>
    /// Trả 404 nếu tài khoản không tồn tại. Chỉ Admin/TaskManager được gọi; Task Manager
    /// không được xem tài khoản Admin/Task Manager khác.
    /// </remarks>
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
        if (!User.IsInRole("Admin") && request.Role is RoleName.Admin or RoleName.TaskManager)
        {
            throw new ForbiddenException(
                "role_escalation_denied", "Chỉ Admin được tạo tài khoản Admin hoặc Task Manager.");
        }

        var created = await userService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = created.User.UserId }, created);
    }

    /// <summary>Sửa họ tên hoặc số điện thoại. Trường nào không gửi thì giữ nguyên; gửi chuỗi rỗng để xoá số điện thoại.</summary>
    /// <remarks>
    /// Trả 404 nếu tài khoản không tồn tại, 403 nếu Task Manager thao tác trên Admin/Task Manager khác.
    /// </remarks>
    [HttpPatch("{id:long}")]
    [ProducesResponseType(typeof(UserDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserDetailDto>> Update(
        long id, [FromBody] UpdateUserRequest request, CancellationToken ct)
    {
        await EnsureCanManageAsync(id, ct);
        return Ok(await userService.UpdateAsync(id, request, ct));
    }

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
    {
        if (!User.IsInRole("Admin"))
        {
            if (request.Role is RoleName.Admin or RoleName.TaskManager)
            {
                throw new ForbiddenException(
                    "role_escalation_denied", "Chỉ Admin được đổi vai sang Admin hoặc Task Manager.");
            }

            await EnsureCanManageAsync(id, ct);
        }

        return Ok(await userService.ChangeRoleAsync(id, request.Role!.Value, User.GetUserId(), ct));
    }

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
    {
        await EnsureCanManageAsync(id, ct);
        return Ok(await userService.LockAsync(id, User.GetUserId(), ct));
    }

    /// <summary>Mở khoá tài khoản.</summary>
    /// <remarks>
    /// Có hiệu lực ngay, người đó có thể đăng nhập lại. Trả 404 nếu tài khoản không tồn tại,
    /// 403 nếu Task Manager mở khoá Admin/Task Manager khác.
    /// </remarks>
    [HttpPost("{id:long}/unlock")]
    [ProducesResponseType(typeof(UserDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserDetailDto>> Unlock(long id, CancellationToken ct)
    {
        await EnsureCanManageAsync(id, ct);
        return Ok(await userService.UnlockAsync(id, ct));
    }

    /// <summary>Cấp lại mật khẩu tạm khi người dùng quên mật khẩu, hoặc khi nghi tài khoản bị lộ.</summary>
    /// <remarks>
    /// Mật khẩu tạm chỉ trả về đúng lần này. Mật khẩu cũ hết tác dụng ngay, và **mọi token người đó đang giữ
    /// bị từ chối** ở request kế tiếp — người đó bị đăng xuất khỏi mọi máy, kể cả máy của người lạ đang cầm token.
    /// </remarks>
    [HttpPost("{id:long}/reset-password")]
    [ProducesResponseType(typeof(ResetPasswordResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ResetPasswordResult>> ResetPassword(long id, CancellationToken ct)
    {
        await EnsureCanManageAsync(id, ct);
        return Ok(await userService.ResetPasswordAsync(id, ct));
    }

    /// <summary>Chủ đề Reviewer này đủ trình độ duyệt nội dung.</summary>
    /// <remarks>
    /// Admin không phải ai cũng hiểu mọi chủ đề, nên mỗi Reviewer được phân những chủ đề họ duyệt được.
    /// Một cặp câu chỉ chuyển sang **đã duyệt** khi có lượt duyệt của người đúng chủ đề.
    /// </remarks>
    [HttpGet("{id:long}/domains")]
    [ProducesResponseType(typeof(IReadOnlyList<string>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<string>>> GetDomains(long id, CancellationToken ct)
        => Ok(await userService.GetDomainsAsync(id, ct));

    /// <summary>Đặt lại toàn bộ chủ đề duyệt của một Reviewer.</summary>
    /// <remarks>Chủ đề nào không còn trong danh sách gửi lên thì bị gỡ. Chỉ áp dụng cho vai Reviewer.</remarks>
    [HttpPut("{id:long}/domains")]
    [ProducesResponseType(typeof(IReadOnlyList<string>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<IReadOnlyList<string>>> SetDomains(
        long id, [FromBody] SetDomainsRequest request, CancellationToken ct)
        => Ok(await userService.ReplaceDomainsAsync(id, request.Domains, ct));

    /// <summary>Task Manager không được đụng vào tài khoản Admin/Task Manager khác.</summary>
    private async Task EnsureCanManageAsync(long id, CancellationToken ct)
    {
        if (User.IsInRole("Admin")) return;

        var target = await userService.GetAsync(id, ct);
        if (target.Role is RoleName.Admin or RoleName.TaskManager)
        {
            throw new ForbiddenException(
                "target_role_denied", "Chỉ Admin được thao tác trên tài khoản Admin hoặc Task Manager.");
        }
    }
}

/// <summary>Người dùng tự quản lý tài khoản của mình.</summary>
[ApiController]
[Route("api/me")]
[Tags(ApiTags.Account)]
[Authorize]
public class MeController(IUserService userService, IConsentService consentService) : ControllerBase
{
    /// <summary>Ghi nhận đồng ý cam kết sử dụng dữ liệu sinh trắc âm thanh của chính mình.</summary>
    /// <remarks>
    /// Lưu vào bảng <c>user_consent</c>. Bắt buộc <c>consentAccepted = true</c> (checkbox xác
    /// nhận tự nguyện).
    ///
    /// <c>consentVersion</c> **không bắt buộc** — bỏ trống thì server ghi theo bản đang hiệu
    /// lực. Có khai thì phải là phiên bản có nội dung thật trong <c>consent_document</c>, nếu
    /// không trả **422 `consent_version_unknown`**. Lấy mã ở `GET /api/consent`.
    /// </remarks>
    [HttpPost("consent")]
    [ProducesResponseType(typeof(ConsentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ConsentDto>> RecordConsent(
        [FromBody] RecordConsentRequest request, CancellationToken ct)
        => Ok(await consentService.RecordConsentAsync(User.GetUserId(), request, ct));

    /// <summary>Lấy thông tin đồng ý cam kết hiện tại của chính mình.</summary>
    /// <remarks>Trả 204 nếu chưa từng đồng ý.</remarks>
    [HttpGet("consent")]
    [ProducesResponseType(typeof(ConsentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult<ConsentDto>> GetConsent(CancellationToken ct)
    {
        var consent = await consentService.GetConsentAsync(User.GetUserId(), ct);
        return consent is null ? NoContent() : Ok(consent);
    }

    /// <summary>Đổi mật khẩu của chính mình — trả về token mới.</summary>
    /// <remarks>
    /// Phải nhập đúng mật khẩu hiện tại. Mật khẩu mới từ 8 ký tự và khác mật khẩu cũ.
    ///
    /// Đổi xong, **mọi token cũ bị từ chối**: token trên máy khác, và cả token vừa gửi request này.
    /// Kết quả có token mới, giống hệt lúc đăng nhập — frontend thay token đang lưu để người dùng ở lại trang.
    /// </remarks>
    [HttpPut("password")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<LoginResponse>> ChangePassword(
        [FromBody] ChangePasswordRequest request, CancellationToken ct)
        => Ok(await userService.ChangeOwnPasswordAsync(User.GetUserId(), request, ct));

    /// <summary>Sửa họ tên, số điện thoại của chính mình. Trường nào không gửi thì giữ nguyên; gửi chuỗi rỗng để xoá số điện thoại.</summary>
    /// <remarks>
    /// Chỉ ảnh hưởng đến tài khoản của người đang đăng nhập. Gửi chuỗi rỗng trong `phoneNumber`
    /// để xoá số điện thoại.
    /// </remarks>
    [HttpPatch]
    [ProducesResponseType(typeof(UserDetailDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<UserDetailDto>> UpdateOwn(
        [FromBody] UpdateUserRequest request, CancellationToken ct)
        => Ok(await userService.UpdateAsync(User.GetUserId(), request, ct));

    /// <summary>
    /// Hồ sơ người đọc của chính mình: năm sinh, tỉnh, vùng miền, trình độ tiếng Anh,
    /// nghề nghiệp, giới tính, chuyên ngành.
    /// </summary>
    /// <remarks>
    /// `region` (<c>North</c>/<c>Central</c>/<c>South</c>) CỐ Ý tách khỏi `province`:
    /// province là ô chữ tự do nên gom nhóm theo nó không đáng tin, còn vùng miền là thứ
    /// dữ liệu giọng nói cần thống kê đúng. Bỏ trống nghĩa là chưa cập nhật.
    /// </remarks>
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

    /// <summary>Chủ đề mình đủ trình độ duyệt — để Reviewer biết mình duyệt được câu thuộc chủ đề nào.</summary>
    /// <remarks>
    /// Chỉ Reviewer được gọi. Danh sách chủ đề dùng để Reviewer biết mình được duyệt câu thuộc
    /// chủ đề nào trong màn duyệt nội dung.
    /// </remarks>
    [HttpGet("domains")]
    [Authorize(Roles = "Reviewer")]
    [ProducesResponseType(typeof(IReadOnlyList<string>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<string>>> GetDomains(CancellationToken ct)
        => Ok(await userService.GetDomainsAsync(User.GetUserId(), ct));
}
