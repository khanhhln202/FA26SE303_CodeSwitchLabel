using CodeSwitchLabel.Repositories.Entities;
using CodeSwitchLabel.Repositories.Enums;
using CodeSwitchLabel.Repositories.Repositories;
using CodeSwitchLabel.Services.Abstractions;
using CodeSwitchLabel.Services.Common;
using CodeSwitchLabel.Services.Dtos;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CodeSwitchLabel.Services.Implementations;

internal static class UserMapper
{
    public static UserListItemDto ToListItem(this AppUser u) =>
        new(u.UserId, u.FullName, u.Email, u.Phone, u.Role.RoleName, u.Status, u.CreatedAt);

    public static SpeakerProfileDto ToDto(this SpeakerProfile? p) =>
        new(p?.BirthYear, p?.Province, p?.EnglishLevel, p?.Occupation, p?.Major);

    /// <remarks>Phải kèm Role và SpeakerProfile.</remarks>
    public static CurrentUserDto ToCurrentUser(this AppUser u) =>
        new(u.UserId, u.Email, u.FullName, u.Role.RoleName.ToString(), u.SpeakerProfile is not null);

    public static LoginResponse ToLoginResponse(this AppUser u, IssuedToken token) =>
        new(token.AccessToken, token.ExpiresAt, u.ToCurrentUser());
}

/// <summary>
/// Quản lý tài khoản. Luật chính:
///   - Chỉ Admin tạo tài khoản; mật khẩu tạm do hệ thống sinh, trả về đúng một lần.
///   - Không xoá tài khoản, chỉ khoá — bản ghi, lượt duyệt, task đều trỏ tới người dùng.
///   - Không bao giờ để hệ thống mất Admin cuối cùng.
///   - Đổi vai bị chặn khi người đó còn đang nhận task chưa xong.
/// Khoá, đổi vai và đổi mật khẩu có hiệu lực ngay vì mỗi request đều đối chiếu token với database
/// (xem AccountStateValidator).
/// </summary>
public class UserService(
    IUserRepository users,
    IPasswordHasher hasher,
    IAccessTokenIssuer tokens,
    TimeProvider clock) : IUserService
{
    public async Task<PagedResult<UserListItemDto>> SearchAsync(
        UserSearchRequest request, CancellationToken ct = default)
    {
        var (items, total) = await users.SearchAsync(
            request.Role, request.Status, request.Keyword, request.Page, request.PageSize, ct);

        return new PagedResult<UserListItemDto>(
            [.. items.Select(u => u.ToListItem())], request.Page, request.PageSize, total);
    }

    public async Task<UserDetailDto> GetAsync(long userId, CancellationToken ct = default)
    {
        var user = await users.GetByIdAsync(userId, ct) ?? throw NotFound(userId);
        var tasks = await users.GetOpenTasksAsync(userId, ct);

        return new UserDetailDto(
            user.UserId,
            user.FullName,
            user.Email,
            user.Phone,
            user.Role.RoleName,
            user.Status,
            user.CreatedAt,
            user.SpeakerProfile is null ? null : user.SpeakerProfile.ToDto(),
            [.. tasks.Select(t => new UserTaskDto(t.TaskId, t.TaskType, t.Description, t.Status))]);
    }

    public async Task<CreateUserResult> CreateAsync(CreateUserRequest request, CancellationToken ct = default)
    {
        // Lưu email chữ thường để một người không thể có hai tài khoản chỉ khác nhau ở chữ hoa.
        var email = request.Email.Trim().ToLowerInvariant();

        if (await users.EmailExistsAsync(email, ct)) throw EmailTaken(email);

        var role = request.Role!.Value;
        var password = TemporaryPassword.Generate();

        var user = new AppUser
        {
            RoleId = await users.GetRoleIdAsync(role, ct),
            FullName = request.FullName.Trim(),
            Email = email,
            Phone = Clean(request.Phone),
            PasswordHash = hasher.Hash(password),
            Status = UserStatus.Active,
            CreatedAt = clock.GetUtcNow()
        };

        // View v_dashboard_summary đếm số người đọc bằng số hồ sơ chứ không bằng vai,
        // nên người đọc nào cũng phải có hồ sơ, kể cả khi chưa điền gì.
        if (role == RoleName.Speaker) user.SpeakerProfile = new SpeakerProfile();

        users.Add(user);

        try
        {
            await users.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Hai request tạo cùng một email đúng cùng lúc: bước kiểm tra phía trên cùng lọt,
            // ràng buộc UNIQUE của database chặn được người đến sau.
            throw EmailTaken(email);
        }

        return new CreateUserResult(await GetAsync(user.UserId, ct), password);
    }

    public async Task<UserDetailDto> UpdateAsync(long userId, UpdateUserRequest request, CancellationToken ct = default)
    {
        var user = await users.GetForUpdateAsync(userId, ct) ?? throw NotFound(userId);

        if (!string.IsNullOrWhiteSpace(request.FullName)) user.FullName = request.FullName.Trim();

        // null là giữ nguyên, chuỗi rỗng là xoá số điện thoại.
        if (request.Phone is not null) user.Phone = Clean(request.Phone);

        await users.SaveChangesAsync(ct);
        return await GetAsync(userId, ct);
    }

    public async Task<UserDetailDto> ChangeRoleAsync(
        long userId, RoleName newRole, long actingUserId, CancellationToken ct = default)
    {
        if (userId == actingUserId)
        {
            throw new ConflictException(
                "cannot_change_own_role", "Không tự đổi vai của chính mình được — nhờ một Admin khác làm việc này.");
        }

        var user = await users.GetForUpdateAsync(userId, ct) ?? throw NotFound(userId);

        if (user.Role.RoleName == newRole)
        {
            throw new ConflictException("role_unchanged", $"{user.Email} đã là {newRole} rồi.");
        }

        await EnsureNotLastAdminAsync(user, ct);

        var openTasks = await users.GetOpenTasksAsync(userId, ct);

        if (openTasks.Count > 0)
        {
            throw new ConflictException(
                "user_has_active_tasks",
                $"{user.Email} đang nhận {openTasks.Count} task chưa xong: " +
                string.Join(", ", openTasks.Select(t => $"#{t.TaskId}")) +
                ". Giao các task đó cho người khác trước rồi mới đổi vai.");
        }

        user.RoleId = await users.GetRoleIdAsync(newRole, ct);

        // Đổi sang người đọc thì tạo hồ sơ nếu chưa có. Đổi sang vai khác thì GIỮ hồ sơ cũ:
        // đó là thông tin gắn với những bản ghi người này đã thu.
        if (newRole == RoleName.Speaker && user.SpeakerProfile is null)
        {
            user.SpeakerProfile = new SpeakerProfile { UserId = user.UserId };
        }

        await users.SaveChangesAsync(ct);
        return await GetAsync(userId, ct);
    }

    public async Task<UserDetailDto> LockAsync(long userId, long actingUserId, CancellationToken ct = default)
    {
        if (userId == actingUserId)
        {
            throw new ConflictException("cannot_lock_self", "Không tự khoá tài khoản của chính mình được.");
        }

        var user = await users.GetForUpdateAsync(userId, ct) ?? throw NotFound(userId);

        // Bấm khoá lần hai không phải lỗi — giao diện nút bật tắt khỏi phải xử lý thêm.
        if (user.Status == UserStatus.Inactive) return await GetAsync(userId, ct);

        await EnsureNotLastAdminAsync(user, ct);

        // Khoá được cả khi người đó đang nhận task: trường hợp khẩn cấp không được bị chặn.
        // Danh sách task trả về trong kết quả để Task Manager biết mà giao lại.
        user.Status = UserStatus.Inactive;

        await users.SaveChangesAsync(ct);
        return await GetAsync(userId, ct);
    }

    public async Task<UserDetailDto> UnlockAsync(long userId, CancellationToken ct = default)
    {
        var user = await users.GetForUpdateAsync(userId, ct) ?? throw NotFound(userId);

        if (user.Status != UserStatus.Active)
        {
            user.Status = UserStatus.Active;
            await users.SaveChangesAsync(ct);
        }

        return await GetAsync(userId, ct);
    }

    public async Task<ResetPasswordResult> ResetPasswordAsync(long userId, CancellationToken ct = default)
    {
        var user = await users.GetForUpdateAsync(userId, ct) ?? throw NotFound(userId);
        var password = TemporaryPassword.Generate();

        // Bản băm mới là dấu mật khẩu mới: mọi token người đó đang giữ bị từ chối ngay ở request kế tiếp.
        // Nhờ vậy cấp lại mật khẩu cũng là cách đuổi người lạ đang cầm token bị lộ.
        user.PasswordHash = hasher.Hash(password);

        await users.SaveChangesAsync(ct);
        return new ResetPasswordResult(userId, password);
    }

    public async Task<LoginResponse> ChangeOwnPasswordAsync(
        long userId, ChangePasswordRequest request, CancellationToken ct = default)
    {
        var user = await users.GetForUpdateAsync(userId, ct) ?? throw NotFound(userId);

        if (!hasher.Verify(request.CurrentPassword, user.PasswordHash))
        {
            throw new UnprocessableException("invalid_current_password", "Mật khẩu hiện tại không đúng.");
        }

        if (request.CurrentPassword == request.NewPassword)
        {
            throw new UnprocessableException("same_password", "Mật khẩu mới phải khác mật khẩu hiện tại.");
        }

        user.PasswordHash = hasher.Hash(request.NewPassword);
        await users.SaveChangesAsync(ct);

        // Từ đây mọi token cũ đều mang dấu của mật khẩu cũ — kể cả token vừa gửi request này — nên bị từ chối.
        // Máy khác bị đăng xuất; riêng máy đang dùng nhận token mới để ở lại trang.
        return user.ToLoginResponse(tokens.Issue(user));
    }

    public async Task<SpeakerProfileDto> GetOwnSpeakerProfileAsync(long userId, CancellationToken ct = default)
    {
        var user = await users.GetByIdAsync(userId, ct) ?? throw NotFound(userId);
        return user.SpeakerProfile.ToDto();
    }

    public async Task<SpeakerProfileDto> UpdateOwnSpeakerProfileAsync(
        long userId, UpdateSpeakerProfileRequest request, CancellationToken ct = default)
    {
        var user = await users.GetForUpdateAsync(userId, ct) ?? throw NotFound(userId);
        var profile = user.SpeakerProfile ??= new SpeakerProfile { UserId = userId };

        profile.BirthYear = request.BirthYear;
        profile.Province = Clean(request.Province);
        profile.EnglishLevel = request.EnglishLevel;
        profile.Occupation = request.Occupation;
        profile.Major = Clean(request.Major);

        await users.SaveChangesAsync(ct);
        return profile.ToDto();
    }

    // ----------------------------------------------------------------- nội bộ

    /// <summary>Hạ vai hay khoá Admin cuối cùng là không còn ai quản trị được hệ thống nữa.</summary>
    private async Task EnsureNotLastAdminAsync(AppUser user, CancellationToken ct)
    {
        if (user.Role.RoleName != RoleName.Admin || user.Status != UserStatus.Active) return;

        if (await users.CountActiveAdminsAsync(ct) <= 1)
        {
            throw new ConflictException(
                "last_admin", "Đây là Admin cuối cùng đang hoạt động. Tạo thêm một Admin khác trước.");
        }
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static NotFoundException NotFound(long userId) =>
        new("user_not_found", $"Không tìm thấy tài khoản #{userId}.");

    private static ConflictException EmailTaken(string email) =>
        new("email_already_exists", $"Email {email} đã có tài khoản.");
}
