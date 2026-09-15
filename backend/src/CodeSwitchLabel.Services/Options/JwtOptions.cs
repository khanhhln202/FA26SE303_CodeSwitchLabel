using System.ComponentModel.DataAnnotations;

namespace CodeSwitchLabel.Services.Options;

public class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required]
    public string Issuer { get; set; } = string.Empty;

    [Required]
    public string Audience { get; set; } = string.Empty;

    /// <summary>
    /// Khoá ký. KHÔNG bao giờ commit giá trị thật vào git —
    /// lúc dev đặt bằng user-secrets, lúc chạy thật đặt bằng biến môi trường.
    /// Bắt tối thiểu 32 ký tự vì HMAC-SHA256 cần khoá đủ dài mới an toàn.
    /// </summary>
    [Required]
    [MinLength(32, ErrorMessage = "Jwt:Key phải dài ít nhất 32 ký tự.")]
    public string Key { get; set; } = string.Empty;

    [Range(1, 1440)]
    public int ExpiryMinutes { get; set; } = 120;
}
