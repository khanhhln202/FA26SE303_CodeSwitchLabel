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

/// <summary>ERD cho mỗi người đúng một vai, nên Role là chuỗi đơn chứ không phải mảng.</summary>
public record CurrentUserDto(
    long UserId,
    string Email,
    string FullName,
    string Role,
    bool HasSpeakerProfile);
