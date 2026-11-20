using System.ComponentModel.DataAnnotations;

namespace CodeSwitchLabel.Services.Dtos;

public record LoginRequest
{
    [Required(ErrorMessage = "Email không được để trống.")]
    [EmailAddress(ErrorMessage = "Email không đúng định dạng.")]
    public string Email { get; init; } = string.Empty;

    [Required(ErrorMessage = "Mật khẩu không được để trống.")]
    public string Password { get; init; } = string.Empty;
}

public record LoginResponse(
    string AccessToken,
    DateTimeOffset ExpiresAt,
    CurrentUserDto User);

public record RegisterSpeakerRequest
{
    [Required(ErrorMessage = "Phải nhập họ tên.")]
    [StringLength(255)]
    public string FullName { get; init; } = string.Empty;

    [Required(ErrorMessage = "Phải nhập email.")]
    [EmailAddress(ErrorMessage = "Email không đúng định dạng.")]
    [StringLength(255)]
    public string Email { get; init; } = string.Empty;

    [Required(ErrorMessage = "Phải nhập mật khẩu.")]
    [StringLength(128, MinimumLength = 8, ErrorMessage = "Mật khẩu phải từ 8 đến 128 ký tự.")]
    public string Password { get; init; } = string.Empty;

    [StringLength(32)]
    public string? Phone { get; init; }

    /// <summary>Hồ sơ tình nguyện: điền luôn lúc đăng ký, không qua duyệt admin.</summary>
    public UpdateSpeakerProfileRequest? SpeakerProfile { get; init; }

    /// <summary>
    /// Đồng ý dùng giọng nói cho nghiên cứu khoa học phi thương mại. Gửi true kèm
    /// <see cref="ConsentVersion"/> để lưu bằng chứng đồng ý ngay lúc tạo tài khoản.
    /// Bản giấy đã ký có thể tải lên sau bằng POST /api/auth/consent.
    /// </summary>
    public bool ConsentAccepted { get; init; }

    [StringLength(32)]
    public string? ConsentVersion { get; init; }
}

/// <summary>Đồng ý phiên bản hiện hành, kèm bản giấy đã ký (PDF/ảnh) nếu có.</summary>
public record AcceptConsentRequest
{
    [Required(ErrorMessage = "Thiếu phiên bản điều khoản.")]
    [StringLength(32)]
    public string Version { get; init; } = string.Empty;
}

/// <summary>ERD cho mỗi người đúng một vai, nên Role là chuỗi đơn chứ không phải mảng.</summary>
public record CurrentUserDto(
    long UserId,
    string Email,
    string FullName,
    string Role,
    bool HasSpeakerProfile);
