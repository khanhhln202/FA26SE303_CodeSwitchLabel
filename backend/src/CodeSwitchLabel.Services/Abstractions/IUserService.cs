using CodeSwitchLabel.Repositories.Enums;
using CodeSwitchLabel.Services.Dtos;

namespace CodeSwitchLabel.Services.Abstractions;

public interface IUserService
{
    Task<PagedResult<UserListItemDto>> SearchAsync(UserSearchRequest request, CancellationToken ct = default);

    Task<UserDetailDto> GetAsync(long userId, CancellationToken ct = default);

    /// <summary>Tạo tài khoản với mật khẩu tạm do hệ thống sinh, trả về đúng một lần.</summary>
    Task<CreateUserResult> CreateAsync(CreateUserRequest request, CancellationToken ct = default);

    Task<UserDetailDto> UpdateAsync(long userId, UpdateUserRequest request, CancellationToken ct = default);

    /// <summary>Đổi vai. Bị chặn nếu người đó còn đang nhận task chưa xong.</summary>
    Task<UserDetailDto> ChangeRoleAsync(long userId, RoleName newRole, long actingUserId, CancellationToken ct = default);

    /// <summary>Khoá tài khoản. Có hiệu lực ngay ở request kế tiếp của người đó.</summary>
    Task<UserDetailDto> LockAsync(long userId, long actingUserId, CancellationToken ct = default);

    Task<UserDetailDto> UnlockAsync(long userId, CancellationToken ct = default);

    Task<ResetPasswordResult> ResetPasswordAsync(long userId, CancellationToken ct = default);

    Task ChangeOwnPasswordAsync(long userId, ChangePasswordRequest request, CancellationToken ct = default);

    Task<SpeakerProfileDto> GetOwnSpeakerProfileAsync(long userId, CancellationToken ct = default);

    /// <summary>Ghi đè toàn bộ hồ sơ: trường nào gửi null là xoá trắng trường đó.</summary>
    Task<SpeakerProfileDto> UpdateOwnSpeakerProfileAsync(
        long userId, UpdateSpeakerProfileRequest request, CancellationToken ct = default);
}
