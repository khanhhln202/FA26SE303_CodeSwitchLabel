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

    /// <summary>Đổi mật khẩu của chính mình. Mọi token cũ bị từ chối; trả token mới cho máy đang dùng.</summary>
    Task<LoginResponse> ChangeOwnPasswordAsync(long userId, ChangePasswordRequest request, CancellationToken ct = default);

    Task<SpeakerProfileDto> GetOwnSpeakerProfileAsync(long userId, CancellationToken ct = default);

    /// <summary>Ghi đè toàn bộ hồ sơ: trường nào gửi null là xoá trắng trường đó.</summary>
    Task<SpeakerProfileDto> UpdateOwnSpeakerProfileAsync(
        long userId, UpdateSpeakerProfileRequest request, CancellationToken ct = default);

    /// <summary>Chủ đề Reviewer này đủ trình độ duyệt nội dung. Rỗng với người không phải Reviewer.</summary>
    Task<IReadOnlyList<ScriptDomain>> GetDomainsAsync(long userId, CancellationToken ct = default);

    /// <summary>Đặt lại toàn bộ chủ đề duyệt của một Reviewer; chủ đề không còn trong danh sách thì bị gỡ.</summary>
    Task<IReadOnlyList<ScriptDomain>> ReplaceDomainsAsync(
        long userId, IReadOnlyList<ScriptDomain> domains, CancellationToken ct = default);
}
