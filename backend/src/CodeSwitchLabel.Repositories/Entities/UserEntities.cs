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
    public UserConsent? Consent { get; set; }
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
    public string? BirthPlace { get; set; }
    public string? Province { get; set; }

    /// <summary>
    /// Vùng miền giọng nói. Không bắt buộc; hồ sơ tạo trước V27 không có giá trị.
    /// Độc lập với <see cref="Province"/> — xem <see cref="SpeakerRegion"/>.
    /// </summary>
    public SpeakerRegion? Region { get; set; }

    /// <summary>Điểm IELTS quy đổi, từ 0.0 đến 9.0.</summary>
    public decimal? EnglishLevel { get; set; }

    public Occupation? Occupation { get; set; }

    /// <summary>Không bắt buộc; hồ sơ tạo trước V19 không có giá trị.</summary>
    public Gender? Gender { get; set; }

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
    public string Domain { get; set; } = string.Empty;
    public DateTimeOffset AssignedAt { get; set; }

    public AppUser User { get; set; } = null!;
}

/// <summary>
/// Đồng ý dùng giọng nói cho nghiên cứu khoa học phi thương mại. Một dòng mỗi người,
/// giữ bản mới nhất; file PDF/ảnh bản đã ký nằm trong object storage.
/// </summary>
public class UserConsent
{
    public long UserId { get; set; }
    public string Version { get; set; } = string.Empty;
    public DateTimeOffset AcceptedAt { get; set; }
    public string? DocumentKey { get; set; }
    public string? DocumentName { get; set; }

    public AppUser User { get; set; } = null!;

    /// <summary>Bản cam kết người này đã đồng ý — đọc lại được nội dung, không chỉ mã phiên bản.</summary>
    public ConsentDocument Document { get; set; } = null!;
}

/// <summary>
/// Nội dung một phiên bản bản cam kết (bảng <c>consent_document</c>, V39).
///
/// <para>
/// Trước V39 hệ thống chỉ lưu mã phiên bản, còn chữ nằm trong mã nguồn frontend — sửa một
/// chữ bên đó là mọi người đã ký <c>v1</c> bỗng "đã đồng ý" một văn bản khác mà không để lại
/// dấu vết nào. Với cam kết về dữ liệu sinh trắc giọng nói, phải đọc lại được chính xác
/// người thứ N đã đồng ý chữ gì, vào lúc nào.
/// </para>
///
/// <para>
/// Phiên bản đã có người đồng ý thì KHÔNG sửa và KHÔNG xoá — đổi chữ là phát hành bản mới.
/// </para>
/// </summary>
public class ConsentDocument
{
    public string Version { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public DateOnly EffectiveFrom { get; set; }

    /// <summary>Đúng một bản hiệu lực mỗi lúc — chỉ mục một phần dưới database giữ luật đó.</summary>
    public bool IsActive { get; set; }

    public long? CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public ICollection<UserConsent> Acceptances { get; set; } = [];
}
