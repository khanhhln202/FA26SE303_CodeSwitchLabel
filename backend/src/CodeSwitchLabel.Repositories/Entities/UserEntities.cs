using CodeSwitchLabel.Repositories.Enums;

namespace CodeSwitchLabel.Repositories.Entities;

/// <summary>
/// Bảng vai. ERD cho mỗi người dùng đúng MỘT vai qua khoá ngoại đơn,
/// không phải quan hệ nhiều-nhiều — nên không dùng cấu trúc bảng của ASP.NET Identity.
/// </summary>
public class Role
{
    public short RoleId { get; set; }
    public RoleName RoleName { get; set; }
    public string? Description { get; set; }

    public ICollection<AppUser> Users { get; set; } = [];
}

public class AppUser
{
    public long UserId { get; set; }

    public short RoleId { get; set; }
    public Role Role { get; set; } = null!;

    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }

    /// <summary>Không bao giờ chứa mật khẩu thô. Băm bằng PasswordHasher của .NET.</summary>
    public string PasswordHash { get; set; } = string.Empty;

    public UserStatus Status { get; set; } = UserStatus.Active;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public SpeakerProfile? SpeakerProfile { get; set; }
}

/// <summary>
/// Hồ sơ người nói, quan hệ 1-1 với AppUser, chỉ có ở tài khoản vai Speaker.
/// Toàn bộ các trường ở đây là biến độc lập cho câu hỏi nghiên cứu RQ2 —
/// chúng không phục vụ chức năng nào, chỉ phục vụ phân tích.
/// </summary>
public class SpeakerProfile
{
    public long UserId { get; set; }
    public AppUser User { get; set; } = null!;

    /// <summary>Năm sinh thay vì tuổi, để không phải cập nhật mỗi năm.</summary>
    public int? BirthYear { get; set; }

    /// <summary>Vùng giọng, không phải nơi ở hiện tại.</summary>
    public SpeakerRegion? Region { get; set; }

    public string? Province { get; set; }
    public EnglishLevel? EnglishLevel { get; set; }
    public Occupation? Occupation { get; set; }
}
