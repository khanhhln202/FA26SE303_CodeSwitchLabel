using CodeSwitchLabel.Repositories.Enums;

namespace CodeSwitchLabel.Repositories.Entities;

public class Role
{
    public short RoleId { get; set; }

    /// <summary>Lưu dạng chuỗi trong database: speaker, reviewer, task_manager, admin.</summary>
    public RoleName RoleName { get; set; }

    public string? Description { get; set; }

    public ICollection<AppUser> Users { get; set; } = [];
}

public class AppUser
{
    public long UserId { get; set; }
    public short RoleId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }

    /// <summary>Chuỗi bcrypt, đúng 60 ký tự — độ rộng cột trong lược đồ.</summary>
    public string PasswordHash { get; set; } = string.Empty;

    public UserStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public Role Role { get; set; } = null!;
    public SpeakerProfile? SpeakerProfile { get; set; }
    public ICollection<UserDomain> Domains { get; set; } = [];
}

/// <summary>
/// Hồ sơ người đọc, một-một với tài khoản. Miền giọng (Bắc/Trung/Nam) KHÔNG lưu:
/// lược đồ suy ra từ tỉnh thành.
/// </summary>
public class SpeakerProfile
{
    public long UserId { get; set; }
    public short? BirthYear { get; set; }
    public string? Province { get; set; }

    /// <summary>Điểm IELTS quy đổi, từ 0.0 đến 9.0.</summary>
    public decimal? EnglishLevel { get; set; }

    public Occupation? Occupation { get; set; }

    /// <summary>Chuyên ngành: IT, english, business…</summary>
    public string? Major { get; set; }

    public AppUser User { get; set; } = null!;
}

/// <summary>
/// Chủ đề một Reviewer đủ trình độ duyệt nội dung. Nhờ bảng này, một cặp câu chỉ được
/// chuyển sang trạng thái validated khi có lượt duyệt của người có đúng chủ đề đó
/// (trigger trg_script_validated_domain của database chặn).
/// </summary>
public class UserDomain
{
    public long UserId { get; set; }
    public ScriptDomain Domain { get; set; }
    public DateTimeOffset AssignedAt { get; set; }

    public AppUser User { get; set; } = null!;
}
