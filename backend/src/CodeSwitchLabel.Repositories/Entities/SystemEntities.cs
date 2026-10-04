using CodeSwitchLabel.Repositories.Enums;

namespace CodeSwitchLabel.Repositories.Entities;

public class SystemConfig
{
    public short ConfigId { get; set; }
    public string ConfigKey { get; set; } = string.Empty;
    public string ConfigValue { get; set; } = string.Empty;
    public ConfigValueType ValueType { get; set; }
    public string? Description { get; set; }

    /// <summary>Null nghĩa là giá trị do hệ thống đặt sẵn, chưa ai sửa.</summary>
    public long? UpdatedBy { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>
/// Khoá cấu hình. Khoá nào có sẵn trong docs/codeswitchlabel.sql giữ nguyên tên theo lược đồ;
/// khoá nào là đề xuất của nhóm backend thì nằm ở DatabaseSeeder — thiếu hàng trong database
/// thì code chạy bằng giá trị mặc định ghi kèm tại chỗ đọc.
/// </summary>
public static class ConfigKeys
{
    public const string ReviewRoundsRequired = "review.rounds_required";
    public const string ReviewDefaultBlind = "review.default_blind";
    public const string RecordingMaxLeadingSilenceSec = "recording.max_leading_silence_sec";
    public const string RecordingMaxTrailingSilenceSec = "recording.max_trailing_silence_sec";
    public const string RecordingAudioFormatDefault = "recording.audio_format_default";
    public const string RecordingMaxTake = "recording.max_take";
    public const string ImportMaxScriptsPerBatch = "import.max_scripts_per_batch";

    /// <summary>Ngưỡng dB coi là khoảng lặng khi phân tích tự động bản ghi.</summary>
    public const string RecordingSilenceNoiseDb = "recording.silence_noise_db";

    /// <summary>Khoảng lặng ngắn hơn mức này (giây) bị bỏ qua khi phân tích tự động.</summary>
    public const string RecordingSilenceMinDurationSec = "recording.silence_min_duration_sec";

    public const string RecordingMinDurationSec = "recording.min_duration_sec";
    public const string RecordingMaxDurationSec = "recording.max_duration_sec";
}

/// <summary>
/// Nhật ký thao tác. Bảng chia mảnh theo tháng và được trigger fn_audit tự ghi;
/// backend chỉ cần đặt biến phiên app.user_id cho mỗi transaction.
/// </summary>
public class AuditLog
{
    public long AuditId { get; set; }
    public DateTimeOffset ChangedAt { get; set; }
    public long? UserId { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public AuditAction Action { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
}

public class Dataset
{
    public long DatasetId { get; set; }
    public string DatasetName { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>Điều kiện lọc lúc tạo, lưu dạng JSON để chọn lại y hệt về sau.</summary>
    public string? FilterCriteria { get; set; }

    public DatasetStatus Status { get; set; }
    public long? ReleasedBy { get; set; }
    public DateTimeOffset? ReleasedAt { get; set; }
    public string? FileKey { get; set; }
    public DatasetFileFormat? FileFormat { get; set; }
    public int RecordingCount { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public ICollection<DatasetRecording> Recordings { get; set; } = [];
}

public class DatasetRecording
{
    public long DatasetId { get; set; }
    public string RecordingId { get; set; } = string.Empty;
    public DateTimeOffset IncludedAt { get; set; }

    public Dataset Dataset { get; set; } = null!;
    public Recording Recording { get; set; } = null!;
}

// ---------------------------------------------------------------------------
// Các view thống kê có sẵn trong lược đồ. Không khoá chính, chỉ đọc.
// ---------------------------------------------------------------------------

public class DashboardSummary
{
    public long TotalCampaigns { get; set; }
    public long OpenCampaigns { get; set; }
    public long ActiveCampaigns { get; set; }
    public long TotalScripts { get; set; }
    public long ValidatedScripts { get; set; }
    public long PendingScripts { get; set; }
    public long TotalRecordings { get; set; }
    public long ApprovedRecordings { get; set; }
    public long RejectedRecordings { get; set; }
    public decimal ApprovedDurationSec { get; set; }
    public long Speakers { get; set; }
    public long Reviewers { get; set; }
    public long ReleasedDatasets { get; set; }
}

public class SpeakerPerformance
{
    public long UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public long Recordings { get; set; }
    public long Approved { get; set; }
    public long Rejected { get; set; }
    public decimal? ApprovalRatePct { get; set; }
}

public class ReviewerPerformance
{
    public long UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public long ReviewsDone { get; set; }
    public long Approvals { get; set; }
    public long Rejections { get; set; }
}

public class RejectionReasonStat
{
    public string ReasonCode { get; set; } = string.Empty;
    public RejectionCategory Category { get; set; }
    public long TimesUsed { get; set; }
}

/// <summary>
/// Tiến độ một chiến dịch: chỉ tiêu, phần đã chia cho các task, phần còn lại, số task và số task xong.
/// Đọc thẳng từ view v_campaign_progress.
/// </summary>
public class CampaignProgress
{
    public long CampaignId { get; set; }
    public string CampaignName { get; set; } = string.Empty;
    public int CampaignTargetQty { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public CampaignStatus CampaignStatus { get; set; }
    public long? AssignedTo { get; set; }
    public long AllocatedTaskQty { get; set; }
    public long RemainingTaskQty { get; set; }
    public long TaskCount { get; set; }
    public long CompletedTaskCount { get; set; }
}
