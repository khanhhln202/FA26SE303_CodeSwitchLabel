using System.Text.Json;
using CodeSwitchLabel.Repositories.Enums;

namespace CodeSwitchLabel.Repositories.Entities;

/// <summary>
/// Một lần xuất dataset. FilterCriteria giữ lại điều kiện lọc lúc xuất,
/// nên sau này tái lập đúng bộ dữ liệu đó được — thứ bắt buộc nếu muốn
/// người khác kiểm chứng lại kết quả nghiên cứu.
/// </summary>
public class Dataset
{
    public long DatasetId { get; set; }

    public string DatasetName { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string? Description { get; set; }

    public JsonDocument? FilterCriteria { get; set; }

    public DatasetStatus Status { get; set; } = DatasetStatus.Draft;

    /// <summary>Ba trường dưới đây chỉ có giá trị sau khi phát hành.</summary>
    public long? ReleasedById { get; set; }
    public AppUser? ReleasedBy { get; set; }
    public DateTimeOffset? ReleasedAt { get; set; }
    public string? FileKey { get; set; }

    public DatasetFileFormat FileFormat { get; set; } = DatasetFileFormat.Json;
    public int RowCount { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<DatasetRecording> Recordings { get; set; } = [];
}

/// <summary>Bản ghi nào nằm trong dataset nào. Chỉ nhận bản ghi đã được duyệt.</summary>
public class DatasetRecording
{
    public long DatasetId { get; set; }
    public Dataset Dataset { get; set; } = null!;

    public long RecordingId { get; set; }
    public Recording Recording { get; set; } = null!;

    public DateTimeOffset IncludedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class Notification
{
    public long NotificationId { get; set; }

    public long RecipientId { get; set; }
    public AppUser Recipient { get; set; } = null!;

    public NotificationType Type { get; set; }

    /// <summary>Nội dung thay đổi theo loại thông báo nên để jsonb, khỏi phải đổi schema mỗi lần thêm loại mới.</summary>
    public JsonDocument? Payload { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>NULL nghĩa là chưa đọc.</summary>
    public DateTimeOffset? ReadAt { get; set; }
}

/// <summary>
/// Nhật ký mọi thay đổi. ERD phân vùng theo tháng để bảng không phình vô hạn —
/// phần phân vùng viết bằng SQL thô trong migration vì EF Core không sinh được.
/// </summary>
public class AuditLog
{
    public long AuditId { get; set; }

    /// <summary>Vừa là một phần khoá chính vừa là khoá phân vùng.</summary>
    public DateTimeOffset ChangedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>NULL nghĩa là hệ thống tự làm, không do người nào.</summary>
    public long? UserId { get; set; }
    public AppUser? User { get; set; }

    public string EntityType { get; set; } = string.Empty;
    public long EntityId { get; set; }
    public AuditAction Action { get; set; }

    public JsonDocument? OldValue { get; set; }
    public JsonDocument? NewValue { get; set; }
}

public class SystemConfig
{
    public short ConfigId { get; set; }
    public string ConfigKey { get; set; } = string.Empty;
    public string ConfigValue { get; set; } = string.Empty;
    public ConfigValueType ValueType { get; set; } = ConfigValueType.String;
    public string? Description { get; set; }

    /// <summary>NULL nghĩa là giá trị khởi tạo của hệ thống, chưa ai sửa.</summary>
    public long? UpdatedById { get; set; }
    public AppUser? UpdatedBy { get; set; }

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>Khoá cấu hình dùng trong code — gom một chỗ để khỏi gõ chuỗi rải rác.</summary>
public static class ConfigKeys
{
    public const string RecordingMinDurationSec = "recording.min_duration_sec";
    public const string RecordingMaxDurationSec = "recording.max_duration_sec";
    public const string ReviewRandomRatio       = "review.random_ratio";
    public const string ScriptMinWordCount      = "script.min_word_count";
    public const string ScriptMaxWordCount      = "script.max_word_count";
    public const string ScriptMinEnWordCount    = "script.min_en_word_count";
}
