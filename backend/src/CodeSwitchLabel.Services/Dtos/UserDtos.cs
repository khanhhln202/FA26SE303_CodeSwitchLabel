using System.ComponentModel.DataAnnotations;
using CodeSwitchLabel.Repositories.Enums;

namespace CodeSwitchLabel.Services.Dtos;

public record UserListItemDto(
    long UserId,
    string FullName,
    string Email,
    string? Phone,
    RoleName Role,
    UserStatus Status,
    DateTimeOffset CreatedAt);

public record SpeakerProfileDto(
    short? BirthYear,
    string? Province,
    decimal? EnglishLevel,
    Occupation? Occupation,
    string? Major);

/// <summary>Task đang chạy mà người dùng này là người nhận.</summary>
public record UserTaskDto(long TaskId, TaskType TaskType, string? Description, WorkTaskStatus Status);

/// <param name="SpeakerProfile">Chỉ có với tài khoản từng là người đọc.</param>
/// <param name="ActiveTasks">Task chưa xong mà người này đang nhận — cần giao lại trước khi đổi vai.</param>
public record UserDetailDto(
    long UserId,
    string FullName,
    string Email,
    string? Phone,
    RoleName Role,
    UserStatus Status,
    DateTimeOffset CreatedAt,
    SpeakerProfileDto? SpeakerProfile,
    IReadOnlyList<UserTaskDto> ActiveTasks);

/// <param name="TemporaryPassword">
/// Mật khẩu tạm, CHỈ trả đúng lần này — database chỉ lưu bản băm. Admin chuyển cho người dùng,
/// người dùng đổi lại bằng PUT /api/me/password.
/// </param>
public record CreateUserResult(UserDetailDto User, string TemporaryPassword);

public record ResetPasswordResult(long UserId, string TemporaryPassword);

public record AssignableUserDto(long UserId, string FullName, RoleName Role, int ActiveTasks, int TotalTarget, decimal? ApprovalRatePct, int OverdueTasks);

public record UserSearchRequest : PageRequest
{
    public RoleName? Role { get; init; }
    public UserStatus? Status { get; init; }

    /// <summary>Tìm theo họ tên hoặc email.</summary>
    public string? Keyword { get; init; }
}

public record CreateUserRequest
{
    [Required(ErrorMessage = "Phải nhập họ tên.")]
    [StringLength(255)]
    public string FullName { get; init; } = string.Empty;

    [Required(ErrorMessage = "Phải nhập email.")]
    [EmailAddress(ErrorMessage = "Email không đúng định dạng.")]
    [StringLength(255)]
    public string Email { get; init; } = string.Empty;

    [StringLength(32)]
    public string? Phone { get; init; }

    [Required(ErrorMessage = "Phải chọn vai: Speaker, Reviewer, TaskManager hoặc Admin.")]
    public RoleName? Role { get; init; }
}

/// <summary>Trường nào để trống thì giữ nguyên.</summary>
public record UpdateUserRequest
{
    [StringLength(255, MinimumLength = 1)]
    public string? FullName { get; init; }

    [StringLength(32)]
    public string? Phone { get; init; }
}

public record ChangeRoleRequest
{
    [Required(ErrorMessage = "Phải chọn vai mới.")]
    public RoleName? Role { get; init; }
}

/// <summary>
/// Đặt lại toàn bộ chủ đề một Reviewer đủ trình độ duyệt. Gửi danh sách rỗng để thu hết chủ đề.
/// </summary>
public record SetDomainsRequest
{
    public List<ScriptDomain> Domains { get; init; } = [];
}

public record ChangePasswordRequest
{
    [Required(ErrorMessage = "Phải nhập mật khẩu hiện tại.")]
    public string CurrentPassword { get; init; } = string.Empty;

    /// <summary>Tối thiểu 8 ký tự — đề xuất của nhóm backend, đề tài không quy định.</summary>
    [Required(ErrorMessage = "Phải nhập mật khẩu mới.")]
    [StringLength(128, MinimumLength = 8, ErrorMessage = "Mật khẩu mới phải từ 8 đến 128 ký tự.")]
    public string NewPassword { get; init; } = string.Empty;
}

/// <summary>Giới hạn lấy đúng theo ràng buộc CHECK của bảng speaker_profile.</summary>
public record UpdateSpeakerProfileRequest
{
    [Range(1900, 2100, ErrorMessage = "Năm sinh phải từ 1900 đến 2100.")]
    public short? BirthYear { get; init; }

    [StringLength(100)]
    public string? Province { get; init; }

    /// <summary>Điểm IELTS quy đổi, từ 0.0 đến 9.0.</summary>
    [Range(typeof(decimal), "0.0", "9.0", ErrorMessage = "Trình độ tiếng Anh phải từ 0.0 đến 9.0 (thang IELTS).")]
    public decimal? EnglishLevel { get; init; }

    public Occupation? Occupation { get; init; }

    [StringLength(100)]
    public string? Major { get; init; }
}
